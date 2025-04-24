using LimparEmail.Domain;
using LimparEmail.Domain.Entities;
using LimparEmail.Domain.Exceptions;
using LimparEmail.Utility;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Net;
using System.Net.Mail;

#region [ROBOT EXECUTION]

string urlBase = string.Empty;
string urlBaseBetweenDates = string.Empty;
int numberOfExecutions;
string nomeOutputCsv = string.Empty;
string nomeOutputLog = string.Empty;

string desiredLabel = string.Empty;
int NumberOfEmailsAccessed = 0;
AppSettings appSettings = new();

const string urlLabel = "#label/";
int ExecucaoLenta = 1;
CurrentIterationPages currentIterationPages = new();
List<string> obtainedEmails = [];
List<DateTime> foundDates = [];

const string csvHeader = $"Visto por;Data da visualização;Origem / Clone;Data da solicitação;Demanda;Email";
const string VistoPor = "Ana Gabriela";
const string OrigemClone = "Brius";
string csvContent = string.Empty;

DateTime requestedDate = new();
DateTime robotStarted = new();
ChromeDriver? driver = null;
IConfiguration config;

try
{
    SettingAppConfig();
    using var sleepBlocker = new SystemSleepBlocker();

    GetDesiredDate();
    InitializeChromeDriver();
    CloseExtraTabs();

    for (int i = 1; i <= numberOfExecutions; i++)
    {
        try
        {
            LogHelper.SalvarLog($"Execução número {i}\r\n\r\n", nomeOutputLog);

            GoToPage(urlBaseBetweenDates);
            DelaySegundos(2);

            if (IsEmptyPage()) 
                break;

            ClickInEmail(1);

            do
            {
                WaitForPageToLoad();
                ProcessEmail();
                GoToNextEmail();

                LogHelper.SalvarLog("\r\n\r\n", nomeOutputLog);

            } while (ContinueExecution());
        }
        catch (Exception ex)
        {
            LogHelper.SalvarLog($"Erro na execução {i} do robô: {ex.Message} - {ex.StackTrace} - " +
                                $"{ex.InnerException}\r\n\r\n", nomeOutputLog);

            if (ex is EndOfExecutionException || i == numberOfExecutions)
                throw;
        }
    }

    SaveEmailsToTheLogs();

    EnviarEmailSucesso();
}
catch (Exception ex)
{
    LogHelper.SalvarLog($"O robô foi finalizado pelo seguite motivo: {ex.Message} - {ex.StackTrace} - {ex.InnerException}", nomeOutputLog);
    SaveEmailsToTheLogs();

    Screenshot? screenshot = null;

    if (driver != null)
        screenshot = ((ITakesScreenshot)driver).GetScreenshot();

    if (appSettings.SendEmail)
    {
        EnviarEmailErro(ex, screenshot);
    }
}
finally
{
    KillDriver(driver);
    Environment.Exit(0);

    LogHelper.SalvarLog("Finalizou a execução do robô", nomeOutputLog);
}

#endregion

#region [CONSOLE METHODS]

void SettingAppConfig()
{
    //NOTA: Isso só funciona com o Production se utilizar corretamente no modo debug, então fica aqui como configurar corretamente:
    //Clique com o botão direito no projeto > Properties > Menu Debug > General > Open Debug Launch Profile UI
    //Em Enviroment Variables, preencha um Name como DOTNET_ENVIRONMENT e na frente o Value como Development
    string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

    config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile($"appSettings.{environment}.json", optional: false, reloadOnChange: true)
        .Build();

    appSettings = config.GetSection("AppSettings").Get<AppSettings>()
                        ?? throw new InvalidOperationException("Erro ao carregar appSettings");

    urlBase = appSettings.UrlBase;
    urlBaseBetweenDates = appSettings.UrlBaseBetweenDates;
    desiredLabel = appSettings.Label;
    numberOfExecutions = appSettings.NumberOfExecutions;
}

void GetDesiredDate()
{
    string? inputDate;
    DateTime validDate;
    bool isValid;

    do
    {
        Console.WriteLine($"OBS: O gmail precisa estar logado no perfil do Chrome antes de rodar o robô\n");
        Console.Write($"MARCADOR QUE O ROBÔ IRÁ RODAR: {desiredLabel}\n\n");

        Console.WriteLine("Digite uma data no formato dd/MM/yyyy: ");
        inputDate = Console.ReadLine();

        isValid = DateTime.TryParseExact(inputDate, "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out validDate) && validDate <= DateTime.Now;

        if (!isValid)
        {
            Console.WriteLine("Formato inválido! Tente novamente.");
            DelaySegundos(1);
            Console.WriteLine("");
        }
        else
        {
            Console.WriteLine("Data válida! Abrindo o navegador...\n");
            DelaySegundos(1);
        }

        Console.Clear();
    } while (!isValid);

    BetweenDates betweenDates = new(validDate, validDate.AddDays(1));
    urlBaseBetweenDates = betweenDates.FormatUrl(urlBaseBetweenDates, appSettings.Label);

    requestedDate = validDate.Date + DateTime.Now.TimeOfDay;

    nomeOutputCsv = $"descadastro_{requestedDate:dd-MM-yyyy_HH-mm-ss}.csv";
    nomeOutputLog = $"descadastro_{requestedDate:dd-MM-yyyy_HH-mm-ss}.txt";

    LogHelper.SalvarCsv(csvHeader, nomeOutputCsv);
}

string GetDesiredLabel()
{
    string? marcadorSelecionado = string.Empty;
    bool isValid;

    do
    {
        Console.WriteLine($"OBS: O gmail precisa estar logado no perfil do Chrome antes de rodar o robô\n");
        Console.Write("Digite o nome do Marcador que o robô deve navegar: ");
        marcadorSelecionado = Console.ReadLine();

        isValid = !string.IsNullOrWhiteSpace(marcadorSelecionado);

        if (!isValid)
        {
            Console.WriteLine("Informe um marcador!");
        }
        else
        {
            Console.WriteLine("");
        }

        Console.Clear();

    } while (!isValid);

    return marcadorSelecionado;
}

void DelaySegundos(int segundos)
{
    Thread.Sleep(1000 * segundos * ExecucaoLenta);
}

#endregion

#region [SELENIUM METHODS]

int ReturnLineNumberOfDesiredDate()
{
    //int startLine = PageNumberToInteracte();
    int startLine = 1;
    LogHelper.SalvarLog("Obteve numero da pagina para interagir.", nomeOutputLog);

    var numberEmailElements = GetAvailableEmailCount();

    for (int i = startLine; i <= numberEmailElements; i++)
    {
        //NOTE: Todas essas duas maneiras abaixo, davam certo até algum ponto, e entre o 2º e 15º email parava de pegar a data. 
        //Com o método ByJS não encontrei mais o erro, então manti ele

        //string spanDate2 = emailElements[i - 1].Text;
        //string spanDate3 = driver.FindElement(By.XPath($"(//td[contains(@class, 'xW') and contains(@class, 'xY')]/span/span)[{i}]")).Text;

        string spanDate = GetSpanDateByIndexWithJS(i);
        LogHelper.SalvarLog($"Obteve data do email na linha {i}", nomeOutputLog);

        DateTime? parseEmailDateToString = ParseEmailStringToDate(spanDate);
        LogHelper.SalvarLog("Transformou o texto em data corretamente", nomeOutputLog);

        DateTime currentEmailDate;

        if (parseEmailDateToString is null)
            throw new EndOfExecutionException($"Padrão de data não reconhecido: {spanDate}. Qtd linhas de span encontradas: {numberEmailElements}");
        else
            currentEmailDate = parseEmailDateToString.Value;

        foundDates.Add(currentEmailDate);

        if (currentEmailDate.Date == requestedDate.Date)
        {
            LogHelper.SalvarLog($"Retornou numero da linha que deve ser clicável: {i}", nomeOutputLog);
            return i;
        }
        else if (!ContinueExecution())
        {
            //throw new EndOfExecutionException($"Foi feita a pesquisa entre as datas {foundDates.Max():dd/MM/yyyy} e {currentEmailDate:dd/MM/yyyy}. ");
            throw new EndOfExecutionException($"Foi feito a pesquisa na data {requestedDate:dd/MM/yyyy}. ");
        }
    }

    return 0;
}

int GetAvailableEmailCount()
{
    string emailsXPath = "//td[contains(@class, 'xW') and contains(@class, 'xY')]/span/span";
    int countEmails;

    try
    {
        countEmails = driver.FindElements(By.XPath(emailsXPath)).Count;
    }
    catch (Exception)
    {
        if (obtainedEmails.Count == 0)
            throw new EndOfExecutionException("Nenhum e-mail encontrado!");

        throw new EndOfExecutionException("Finalizou a execução!");
    }

    if (countEmails > 0)
    {
        return countEmails;
    }
    else if (obtainedEmails.Count == 0)
    {
        throw new EndOfExecutionException("Nenhum e-mail encontrado!");
    }

    throw new EndOfExecutionException("Finalizou a execução!");
}

void ProcessEmail()
{
    PopulateCsv();
    RemoveLabels();
}

void GoToNextEmail()
{
    ClickWithWait(By.XPath("//*[@class='T-I J-J5-Ji adg T-I-awG T-I-ax7 T-I-Js-Gs L3']"));

    NumberOfEmailsAccessed++;
    LogHelper.SalvarLog("Avançou para o próximo e-mail", nomeOutputLog);
}

void InitializeChromeDriver()
{
    robotStarted = DateTime.Now;
    ChromeOptions options = new();

    options.BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    string userDataDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\AppData\\Local\\Google\\Chrome\\User Data";
    var devToolsPortFile = Path.Combine(userDataDir, "DevToolsActivePort");

    if (File.Exists(devToolsPortFile))
        File.Delete(devToolsPortFile);

    string userDataDirOption = $"user-data-dir={userDataDir}";
    options.AddArgument(userDataDirOption);

    //chrome://version/ -> Use para obter qual profile informar, na opção Caminho do perfil
    string profileDirectory = appSettings.ProfileFolder;
    options.AddArgument(profileDirectory);

    options.AddArgument("--disable-gpu"); // Desativa a aceleração de hardware via GPU, útil para evitar problemas gráficos.
    options.AddArgument("--disable-extensions"); // Desativa extensões do navegador, melhorando desempenho e estabilidade.
    options.AddArgument("--start-maximized"); // Inicia o navegador maximizado para melhor visualização.
    options.AddExcludedArgument("enable-automation"); // Remove a flag de automação
    options.AddAdditionalOption("useAutomationExtension", false); // Desativa extensões de automação

    options.AddArgument("--disable-popup-blocking"); // Impede o bloqueio de pop-ups, útil para testes que precisam interagir com eles.
    //options.AddArgument("--ignore-certificate-errors"); // Ignora erros de certificados SSL, útil para ambientes de teste.
    //options.AddArgument("--blink-settings=imagesEnabled=false"); // Desativa o carregamento de imagens para melhorar desempenho.
    //options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.popups", 2);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.plugins", 2);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.notifications", 2);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.automatic_downloads", 2);

    driver = new ChromeDriver(options);
    LogHelper.SalvarLog("Inicializou o Chrome", nomeOutputLog);
}

void KillDriver(IWebDriver? driver)
{
    driver.Quit();

    LogHelper.SalvarLog("Finalizou o Chrome", nomeOutputLog);

    //Reinicia o driver com as configs resetadas. Por enquanto o codigo abaixo não está sendo necessário pois não estou utilizando a config de remover imagens
    //ChromeOptions options = new();
    //options.BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    //options.AddArgument("user-data-dir=C:\\Users\\matheus.pires\\AppData\\Local\\Google\\Chrome\\User Data");
    //options.AddArgument("profile-directory=Default");

    //options.AddUserProfilePreference("profile.default_content_setting_values.images", 1);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.notifications", 1);
    //options.AddUserProfilePreference("profile.managed_default_content_settings.automatic_downloads", 1);

    //driver = new ChromeDriver(options);

    //driver.Quit();
}

void WaitForPageToLoad(int secondsToWait = 10)
{
    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(secondsToWait));

    wait.Until(driver => ((IJavaScriptExecutor)driver)
        .ExecuteScript("return document.readyState").Equals("complete"));
}

void GoToNextEmailPage()
{
    int currentPage = GetPageNumber();
    GoToPageNumber(currentPage + 1);

    if (IsEmptyPage()) throw new EndOfExecutionException("Chegou ao final das páginas possíveis");
}

void GoToPreviousEmailPage()
{
    int currentPage = GetPageNumber();

    if (currentPage > 1)
    {
        GoToPageNumber(currentPage - 1);
    }

    if (IsEmptyPage()) throw new EndOfExecutionException("Chegou ao final das páginas possíveis");
}

int GetPageNumber()
{
    string currentUrlPage = ReturnUrlPage();

    if (currentUrlPage == desiredLabel)
        return 1;

    if (int.TryParse(currentUrlPage.AsSpan(currentUrlPage.LastIndexOf('p') + 1), out int pageNumber))
        return pageNumber;

    return 1;
}

void GoToPageNumber(int pageNumber)
{
    GoToPage($"{urlBase}{urlLabel}{desiredLabel}/p{pageNumber}");
}

string ReturnUrlPage()
{
    return driver.Url[(driver.Url.LastIndexOf('/') + 1)..];
}

/// <summary>
/// Navega até o link desejado e aguarda 2 segundos
/// </summary>
void GoToPage(string desiredPage)
{
    driver.Navigate().GoToUrl(desiredPage);
    WaitForPageToLoad(4);

    LogHelper.SalvarLog($"Acessou a página {desiredPage}", nomeOutputLog);
}

DateTime? ParseEmailStringToDate(string dateString)
{
    var monthMapping = new Dictionary<string, int>
        {
            { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
            { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
        };

    var currentDate = DateTime.Today;
    var culture = new CultureInfo("pt-BR");

    if (TimeSpan.TryParse(dateString, out var time))
    {
        if (time < robotStarted.TimeOfDay)
            return currentDate.Add(time);
        else
            return currentDate.AddDays(-1).Add(time);
    }
    else if (dateString.Contains(" de "))
    {
        var parts = dateString.Split(" de ");

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int day) &&
            monthMapping.TryGetValue(parts[1].TrimEnd('.').ToLower(), out int month))
        {
            return new DateTime(currentDate.Year, month, day);
        }
    }
    else if (DateTime.TryParseExact(dateString, "dd/MM/yyyy", culture, DateTimeStyles.None, out var parsedDate))
    {
        return parsedDate;
    }

    return null;
}

void WaitUntilDisplayedAndEnabled(By by)
{
    WebDriverWait wait = new(driver, TimeSpan.FromSeconds(5));

    IWebElement element = wait.Until(driver =>
    {
        IWebElement el = driver.FindElement(by);
        return (el.Displayed && el.Enabled) ? el : null;
    });
}

/// <summary>
/// Verifica se um elemento está visível e clicável na tela.
/// </summary>
/// <param name="driver">Instância do WebDriver</param>
/// <param name="by">Localizador do elemento (By.Id, By.XPath, etc.)</param>
/// <param name="timeoutInSeconds">Tempo máximo de espera (opcional)</param>
/// <returns>True se o elemento for visível e clicável, False caso contrário.</returns>
bool IsElementVisibleAndClickable(By by, int timeoutInSeconds = 2)
{
    try
    {
        WebDriverWait wait = new(driver, TimeSpan.FromSeconds(timeoutInSeconds));

        IWebElement element = wait.Until(drv => drv.FindElements(by).FirstOrDefault());

        return element != null && element.Displayed && element.Enabled;
    }
    catch (WebDriverTimeoutException)
    {
        return false;
    }
    catch (NoSuchElementException)
    {
        return false;
    }
}

IWebElement WaitForElement(By by, int tempo = 2)
{
    var cont = 0;
    int espera = tempo * 1000;
    Thread.Sleep(1000);

    while (cont < espera)
    {
        try
        {
            var element = driver.FindElement(by);

            if (element != null)
            {
                return element;
            }
        }
        catch (NoSuchElementException)
        {
            // Se o elemento não for encontrado, continue a espera
        }

        cont += 500;
        Thread.Sleep(1000);
    }

    return null;
}

/// <summary>
/// Atualiza os valores da iteração atual das páginas.
/// </summary>
void UpdateCurrentIterationPages()
{
    const string className = "ts";

    if (!IsElementVisibleAndClickable(By.ClassName(className)))
        return;

    var elements = driver.FindElements(By.ClassName(className));

    if (elements.Count != 3)
        throw new Exception($"Erro ao buscar páginas: esperado 3, mas retornou {elements.Count}.");

    List<int> pages = [.. elements
        .Select(element => Convert.ToInt32(element.Text))
        .OrderBy(x => x)];

    currentIterationPages.currentPageStart = pages[0];
    currentIterationPages.currentPageFinish = pages[1];
    currentIterationPages.totalEmails = pages[2];
}

int PageNumberToInteracte()
{
    //É o seguinte... na primeira página precisa começar a iteragir a partir da linha 1. 
    //A partir da segunda página tem que começar a interagir da linha 51
    if (GetPageNumber() == 1)
        return 1;
    else
        return 51;
}

bool IsEmptyPage()
{
    if (IsElementVisibleAndClickable(By.XPath("//*[@class='TD']/*[@class='TC']"), 2))
    {
         var teste = driver.FindElement(By.XPath("//*[@class='TD']/*[@class='TC']"))
                           .Text
                           .Replace("\"", "")  
                           .Replace("\r\n", ""); 

        var teste2 = teste.Contains("Nenhuma mensagem correspondeu");

        return teste2;
    }

    return false;
}

void CloseExtraTabs()
{
    var allTabs = driver.WindowHandles;
    var mainTab = allTabs.First();

    foreach (var tab in allTabs.Skip(1))
    {
        driver.SwitchTo().Window(tab);
        driver.Close();
    }

    driver.SwitchTo().Window(mainTab);
}

/// <summary>
/// Continua execução somente se não é uma página vazia OU 
/// se a ultima data encontrada é diferente da solicitada e já encontrou algum e-mail válido. 
/// Isso significa que o robô já começou a buscar e terminou de buscar emails da data atual
/// </summary>
bool ContinueExecution()
{
    //REGRA ANTIGA
    //return !IsEmptyPage() || (obtainedEmails.Count != 0 && foundDates.Last() != requestedDate);

    //TODO: verificar se o marcador do botão de avançar está disabled
    return !IsNextEmailButtonDisabled() && !IsEmptyPage();
}

bool IsNextEmailButtonDisabled()
{
    return IsElementVisibleAndClickable(By.XPath("//*[@class='T-I J-J5-Ji adg T-I-awG RwtgC T-I-ax7 T-I-Js-Gs T-I-JE L3']"));
}

void ClickInEmail(int lineNumberToClick)
{
    ClickWithWait(By.XPath($"(//*[@class='yX xY '])[{lineNumberToClick}]"));
    DelaySegundos(2);
    NumberOfEmailsAccessed++;
}

void PopulateCsv()
{
    string emailSender = GetEmailSender();

    if (string.IsNullOrWhiteSpace(emailSender))
    {
        throw new NoSuchElementException("Não foi encontrado email remetente. " +
                                         "Favor conferir seletores do robô.");
    }

    //Solução criada para evitar ficar infinitamente no loop
    if (obtainedEmails.Count >= 5 &&
        obtainedEmails.TakeLast(5).All(item => item == emailSender))
    {
        throw new Exception("Caiu no loop. Será reiniciado a execução");
    }

    if (!obtainedEmails.Contains(emailSender))
    {
        csvContent = GenerateCsvRow(requestedDate, emailSender);
        LogHelper.SalvarCsv(csvContent, nomeOutputCsv);
    }

    obtainedEmails.Add(emailSender);

    LogHelper.SalvarLog($"Populou o csv com o email do remetente.", nomeOutputLog);
}

void RemoveLabels()
{
    var numberOfLabels = driver.FindElements(By.XPath("//*[@class='ahR']")).Count;

    for (int i = 1; i <= numberOfLabels; i++)
    {
        bool clickHappened = false;
        var iterator = 1;

        do
        {
            var currentLabel = WaitForElement(By.XPath($"//*[@class='ahR'][{iterator}]/span[1]/div[1]")).Text;

            //Se é a última label (só tem mais uma) e o texto não é igual a label desejado
            //É pq caiu no erro do Externa, e lança uma execeção
            if (i == numberOfExecutions &&
                currentLabel != desiredLabel)
            {
                throw new Exception($"Só tem a label {currentLabel} disponível para remover e não é a mesmo que a label desejada {desiredLabel}");
            }

            bool isLastLabel = i == numberOfLabels;
            bool isDesiredLabel = currentLabel == desiredLabel;

            if ((isLastLabel && isDesiredLabel) || (!isLastLabel && !isDesiredLabel))
            {
                ClickWithWait(By.XPath($"(//*[@class='wYeeg'])[{iterator}]"));
                clickHappened = true;

                if (!isLastLabel) // Só espera se ainda houver mais marcadores
                    DelaySegundos(1);
            }

            iterator++;
        } while (!clickHappened);
    }

    LogHelper.SalvarLog("Clicou em todos os marcadores e voltou uma página", nomeOutputLog);

    WaitForPageToLoad(3);
}

string GetEmailSender()
{
    var selectors = new (By Selector, Func<IWebElement, string> ExtractEmail)[]
    {
        (By.ClassName("go"), e => e.Text[1..^1]),  // Remove o primeiro e o último caractere
        (By.XPath("//*[@class='gD']/span"), e => e.Text),
        (By.XPath("//*[@class='gD']"), e => e.GetAttribute("email"))
    };

    foreach (var (selector, extractEmail) in selectors)
    {
        if (IsElementVisibleAndClickable(selector, 2))
        {
            string email = extractEmail(driver.FindElement(selector));

            if (Util.IsValidEmail(email))
                return email.Trim();
        }
    }

    return string.Empty;
}

/// <summary>
/// O preenchimento precisa seguir os valores do cabeçalho da variável >> csvHeader:
/// Visto por, Data da visualização, Origem / Clone, Data da solicitação, Demanda
/// </summary>
string GenerateCsvRow(DateTime emailDate, string email)
{
    return $"{VistoPor};{DateTime.Now:dd/MM/yyyy};{OrigemClone};" +
           $"{emailDate:dd/MM/yyyy};Descadastro;{email}";
}

string GetSpanDateByIndexWithJS(int index)
{
    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JsScripts/GetSpanDateByIndex.js");
    string script = File.ReadAllText(scriptPath);

    return (string)ExecuteJavaScript($"{script} return GetSpanDateByIndex(arguments[0]);", index);
}

bool ClickUsingJS(string xpath)
{
    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JsScripts/ClickByXPath.js");
    string script = File.ReadAllText(scriptPath);

    return (bool)ExecuteJavaScript($"{script} return ClickByXPath(arguments[0]);", xpath);
}

void ClickWithWait(By by)
{
    if (IsElementVisibleAndClickable(by, 5))
    {
        driver.FindElement(by).Click();
    }
    else
    {
        throw new Exception($"Não foi possível realizar clique no elemento {by}");
    }
}

object ExecuteJavaScript(string script, params object[] args)
{
    IJavaScriptExecutor jsExecutor = driver;
    return jsExecutor.ExecuteScript(script, args);
}

void SaveEmailsToTheLogs()
{
    LogHelper.SalvarLog("\r\n" + string.Join(" | ", obtainedEmails) + "\r\n", nomeOutputLog);
}

#endregion

#region [EMAIL METHODS]

void EnviarEmail(string subject, Exception ex = null, MemoryStream print = null)
{
    using MailMessage message = new();

    if (ex is EndOfExecutionException)
    {
        message.Body = $"<h2>Log da execução do robô: <br /><br />";
    }
    else if (ex is Exception)
    {
        message.Body = $"<h2>Erro encontrado na execução do robô. Verifique o log em anexo! <br /><br />";
    }
    else
    {
        message.Body = $"<h2>Sucesso na execução do robô. <br /><br />";
    }

    List<string> mailAddressTo = [.. appSettings.RecipientEmails.Split(";")];

    foreach (string mailAddress in mailAddressTo)
    {
        message.To.Add(new MailAddress(mailAddress));
    }

    message.Subject = subject;
    message.IsBodyHtml = true;

    SmtpClient smtp = new("smtp.gmail.com", 587)
    {
        UseDefaultCredentials = false,
        Credentials = new NetworkCredential("matheuswith@gmail.com", appSettings.SmtpPassword),
        EnableSsl = true
    };

    //LOG
    if (ex is not null)
    {
        string caminhoLog = Path.Combine(AppContext.BaseDirectory, "Output", nomeOutputLog);

        if (File.Exists(caminhoLog))
        {
            Attachment attachment = new(caminhoLog);
            message.Attachments.Add(attachment);
        }
    }

    //CSV
    if (!string.IsNullOrWhiteSpace(csvContent))
    {
        string caminhoCsv = Path.Combine(AppContext.BaseDirectory, "Output", nomeOutputCsv);

        if (File.Exists(caminhoCsv))
        {
            Attachment attachment = new(caminhoCsv);
            message.Attachments.Add(attachment);
        }
        else
        {
            LogHelper.SalvarLog("Arquivo CSV não encontrado para envio.", nomeOutputLog);
        }

        if (print is not null)
        {
            print.Position = 0;
            message.Attachments.Add(new Attachment(print, "screenshot.png", "image/png"));
        }

        message.Body += $"<br /><br />{obtainedEmails.Count} emails foram descadastrados de {NumberOfEmailsAccessed} emails acessados, " +
                        $"já evitando repetição de emails.<br />" +
                        $"Segue em anexo arquivo .csv com emails descadastrados referente ao dia {requestedDate:dd/MM/yyyy}."
                        ;
    }
    else
    {
        message.Body += "Nenhum e-mail foi encontrado para esta data.";
    }

    message.From = new MailAddress("matheuswith@gmail.com");

    try
    {
        smtp.Send(message);
    }
    finally
    {
        foreach (Attachment attach in message.Attachments)
        {
            attach.ContentStream?.Dispose();
        }
    }

    LogHelper.SalvarLog("Enviou e-mail", nomeOutputLog);
}

void EnviarEmailSucesso()
{
    string subject = "EMAILS DESCADASTRADOS";

    EnviarEmail(subject, null, null);
}

void EnviarEmailErro(Exception ex, Screenshot? screenshot)
{
    string subject = "ROBÔ DESCADASTRO";

    if (screenshot?.AsByteArray != null)
    {
        using MemoryStream print = new(screenshot.AsByteArray);
        EnviarEmail(subject, ex, print);
    }
    else
    {
        EnviarEmail(subject, ex, null);
    }
}

#endregion
