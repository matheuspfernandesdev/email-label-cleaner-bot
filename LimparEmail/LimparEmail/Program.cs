using LimparEmail.Domain;
using LimparEmail.Domain.Exceptions;
using LimparEmail.Utility;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;

#region [ROBOT EXECUTION]

//PRD - Sim, estou com preguiça! É só um projeto de final de semana (que me tomou 5 dias :D), então me deixa! 
//const string urlBase = "https://mail.google.com/mail/u/5/";

//DEV
const string urlBase = "https://mail.google.com/mail/u/0/";

const string urlLabel = "#label/";
int ExecucaoLenta = 1;
CurrentIterationPages currentIterationPages = new();
List<string> unsubscribeList = [];
List<string> obtainedEmails = [];
List<DateTime> foundDates = [];

const string csvHeader = "Visto por;Data da visualização;Origem / Clone;Data da solicitação;Demanda;Email\n";
const string VistoPor = "Ana Gabriela";
const string OrigemClone = "Brius";
string csvContent = string.Empty;

string desiredLabel = "Recibos";
DateTime requestedDate = new();

ChromeDriver? driver = null;

try
{
    desiredLabel = GetDesiredLabel();
    requestedDate = GetDesiredDate();

    driver = InitializeChromeDriver();

    CloseExtraTabs();

    GoToPage(urlBase + urlLabel + desiredLabel + "/p1");

    do
    {
        var lineNumberToClick = ReturnLineNumberOfDesiredDate();

        if (lineNumberToClick != 0)
        {
            GetInfoFromEmail(lineNumberToClick);
        }
        else
        {
            GoToNextEmailPage();
        }
    } while (ContinueExecution());

    EnviarEmailSucesso();
}
catch (Exception ex)
{
    EnviarEmailErro(ex);
}
finally
{
    KillDriver(driver);
}

#endregion

#region [CONSOLE METHODS]

DateTime GetDesiredDate()
{
    string? inputDate;
    DateTime validDate;
    bool isValid;

    do
    {
        Console.WriteLine($"OBS: O gmail precisa estar logado no perfil do Chrome antes de rodar o robô\n");
        Console.Write($"MARCADOR ESCOLHIDO: {desiredLabel}\n\n");

        Console.WriteLine("Digite uma data no formato dd/MM/yyyy: ");
        inputDate = Console.ReadLine();

        isValid = DateTime.TryParseExact(inputDate, "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out validDate) && validDate.Date <= DateTime.Now.Date;

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

    return validDate;
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

#endregion

#region [SELENIUM METHODS]

int ReturnLineNumberOfDesiredDate()
{
    int startLine = PageNumberToInteracte();

    DelaySegundos(2);
    var emailElements = driver.FindElements(By.XPath("//td[contains(@class, 'xW') and contains(@class, 'xY')]/span/span"))
                                    ?? throw new Exception("Nenhum e-mail encontrado.");

    for (int i = startLine; i <= emailElements.Count; i++)
    {
        string spanDate = emailElements[i - 1].Text;

        //spanDate = driver.FindElement(By.XPath($"(//td[contains(@class, 'xW') and contains(@class, 'xY')]/span/span)[{i}]")).Text;

        DateTime? parseEmailDateToString = ParseEmailStringToDate(spanDate);
        DateTime currentEmailDate;

        if (parseEmailDateToString is null)
            throw new Exception($"Padrão de data não reconhecido: {spanDate}. Qtd linhas de span encontradas: {emailElements.Count()}");
        else
            currentEmailDate = parseEmailDateToString.Value;

        foundDates.Add(currentEmailDate);

        if (currentEmailDate.Date == requestedDate.Date)
        {
            return i;
        }
        //A lógica é: o e-mail sempre estar ordenado da maior data para a menor
        //Se chegar numa data do e-mail que é menor que data solicitada, é porque não tem como mais encontrar e-mail com essa data referente
        else if (!ContinueExecution())
        {
            throw new ValidationException($"Foi feita a pesquisa entre as datas {foundDates.Max():dd/MM/yyyy} e {currentEmailDate:dd/MM/yyyy}. ");
        }
    }

    return 0;
}

void GetInfoFromEmail(int lineNumberToClick)
{
    driver.FindElement(By.XPath($"(//*[@class='Cp']//tbody/tr)[{lineNumberToClick}]")).Click();
    WaitForPageToLoad(5);

    PopulateCsv();

    var numberOfLabels = driver.FindElements(By.XPath("//*[@class='ahR']")).Count;

    for (int i = 1; i <= numberOfLabels; i++)
    {
        bool clickHappened = false;
        var iterator = 1;

        do
        {
            var currentLabel = driver.FindElement(By.XPath($"//*[@class='ahR'][{iterator}]/span[1]/div[1]")).Text;
            bool isLast = i == numberOfLabels;

            if (currentLabel == desiredLabel && isLast)
            {
                //Só clica no X do marcador desejado, se for o ultimo
                DelaySegundos(1);
                driver.FindElement(By.XPath($"(//*[@class='wYeeg'])[{iterator}]")).Click();
                clickHappened = true;
                DelaySegundos(2);
            }
            else if (currentLabel != desiredLabel && !isLast)
            {
                //Só clica no X dos outros marcadores, se for antes do ultimo
                driver.FindElement(By.XPath($"(//*[@class='wYeeg'])[{iterator}]")).Click();
                clickHappened = true;
                DelaySegundos(1);
            }

            iterator++;
        } while (!clickHappened);
    }

    WaitForPageToLoad(5);
}

ChromeDriver InitializeChromeDriver()
{
    var options = new ChromeOptions();

    var driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Drivers");
    options.BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    options.AddArgument($"user-data-dir={Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\AppData\\Local\\Google\\Chrome\\User Data");

    //DEV
    options.AddArgument("profile-directory=Default");
    //PRD
    //options.AddArgument("profile-directory=Profile 7");

    //options.AddArgument("--headless=new"); 
    options.AddArgument("--disable-gpu");
    options.AddArgument("--disable-extensions");
    options.AddArgument("--disable-infobars");
    options.AddArgument("--start-maximized");
    options.AddArgument("--disable-popup-blocking");
    options.AddArgument("--ignore-certificate-errors");
    options.AddArgument("--blink-settings=imagesEnabled=false");
    options.AddArgument("--disable-blink-features=AutomationControlled");
    options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
    options.AddUserProfilePreference("profile.managed_default_content_settings.popups", 2);
    options.AddUserProfilePreference("profile.managed_default_content_settings.plugins", 2);
    options.AddUserProfilePreference("profile.managed_default_content_settings.notifications", 2);
    options.AddUserProfilePreference("profile.managed_default_content_settings.automatic_downloads", 2);

    var driverService = ChromeDriverService.CreateDefaultService(driverPath);
    return new ChromeDriver(driverService, options);
}

void KillDriver(IWebDriver? driver)
{
    driver.Quit();

    //Reinicia o driver com as configs resetadas
    ChromeOptions options = new();
    options.BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    options.AddArgument("user-data-dir=C:\\Users\\matheus.pires\\AppData\\Local\\Google\\Chrome\\User Data");
    options.AddArgument("profile-directory=Default");

    options.AddUserProfilePreference("profile.default_content_setting_values.images", 1);
    options.AddUserProfilePreference("profile.managed_default_content_settings.notifications", 1);
    options.AddUserProfilePreference("profile.managed_default_content_settings.automatic_downloads", 1);

    driver = new ChromeDriver(options);

    driver.Quit();
}

void WaitForPageToLoad(int secondsToWait = 10)
{
    DelaySegundos(1);
    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(secondsToWait));

    wait.Until(driver => ((IJavaScriptExecutor)driver)
        .ExecuteScript("return document.readyState").Equals("complete"));
}

void GoToNextEmailPage()
{
    int currentPage = GetPageNumber();
    GoToPageNumber(currentPage + 1);

    if (IsLabelEmptyPage()) throw new ValidationException("Chegou ao final das páginas possíveis");
}

void GoToPreviousEmailPage()
{
    int currentPage = GetPageNumber();

    if (currentPage > 1)
    {
        GoToPageNumber(currentPage - 1);
    }

    if (IsLabelEmptyPage()) throw new ValidationException("Chegou ao final das páginas possíveis");
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
    WaitForPageToLoad();
    IsLabelEmptyPage();
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
        return currentDate.Add(time);
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

bool IsLabelEmptyPage()
{
    //Element that shows the text "Não existem conversas com este marcador."
    return IsElementVisibleAndClickable(By.XPath("//*[@class='TC']"));
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
/// Continua execução somente se a data solitada for menor que a ultima data encontrada. 
/// Quer dizer que ainda existe data 
/// </summary>
bool ContinueExecution()
{
    return requestedDate.Date <= foundDates.Min();
}

void PopulateCsv()
{
    string emailSender = GetEmailSender();

    if (string.IsNullOrWhiteSpace(emailSender))
    {
        throw new NoSuchElementException("Não foi encontrado email remetente. " +
                                         "Favor conferir seletores do robô.");
    }
    
    //Salvo no CSV somente e-mail ainda não obtidos. Não salvo repetidos
    if (!obtainedEmails.Contains(emailSender))
    {
        obtainedEmails.Add(emailSender);

        if (string.IsNullOrWhiteSpace(csvContent)) csvContent = csvHeader;

        csvContent += GenerateCsvRow(foundDates.Last(), emailSender);
    }
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
        if (IsElementVisibleAndClickable(selector, 1))
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
           $"{emailDate:dd/MM/yyyy};Descadastro;{email}\n";
}


#endregion

#region [EMAIL METHODS]


void EnviarEmail(string messageBody, string subject)
{
    using MailMessage message = new();

    //PRD
    //message.To.Add(new MailAddress("anagabriela.campos@hotmail.com"));
    //message.To.Add(new MailAddress("bibianagabriela@gmail.com"));
    //DEV
    message.To.Add(new MailAddress("matheuswith51@hotmail.com"));

    message.Subject = subject;
    message.Body = messageBody;
    message.IsBodyHtml = true;

    SmtpClient smtp = new("smtp.gmail.com", 587)
    {
        UseDefaultCredentials = false,
        Credentials = new NetworkCredential("matheuswith@gmail.com", "bmabplzvhlagqdoy"),
        EnableSsl = true
    };

    if (!string.IsNullOrWhiteSpace(csvContent))
    {
        byte[] bom = Encoding.UTF8.GetPreamble();
        byte[] csvBytes = Encoding.UTF8.GetBytes(csvContent);
        byte[] finalBytes = [.. bom, .. csvBytes];

        MemoryStream csvStream = new MemoryStream(finalBytes);
        Attachment attachment = new(csvStream, $"descadastro_{requestedDate:dd-MM-yyyy}.csv", "text/csv");

        message.Attachments.Add(attachment);

        message.Body += $"Em anexo arquivo .csv com os emails descadastrados " +
                        $"referente ao dia {requestedDate:dd/MM/yyyy}<br />";
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
}

void EnviarEmailSucesso()
{
    string msgBody = $"<h2>Sucesso na execução do robô de descadastro. ";

    string subject = "EMAILS DESCADASTRADOS";

    EnviarEmail(msgBody, subject);
}

void EnviarEmailErro(Exception ex)
{
    string msgBody;

    if (ex is ValidationException)
    {
        msgBody = $"<h2>Log da execução do robô: <br /><br />" +
                         $"{WebUtility.HtmlEncode(ex.Message)} ";
    }
    else
    {
        msgBody = $"<h2>Erro encontrado na execução do robô: <br /><br />" +
                         $"{WebUtility.HtmlEncode(ex.Message)} ";
    }

    string subject = "ROBÔ DESCADASTRO";

    EnviarEmail(msgBody, subject);
}

void DelaySegundos(int segundos)
{
    Thread.Sleep(1000 * segundos * ExecucaoLenta);
}

#endregion
