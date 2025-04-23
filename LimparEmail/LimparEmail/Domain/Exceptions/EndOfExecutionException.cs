namespace LimparEmail.Domain.Exceptions
{
    [Serializable]
    public class EndOfExecutionException : Exception
    {
        public EndOfExecutionException() : base("Validation error occurred.") { }

        public EndOfExecutionException(string message) : base(message) { }

        public EndOfExecutionException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
