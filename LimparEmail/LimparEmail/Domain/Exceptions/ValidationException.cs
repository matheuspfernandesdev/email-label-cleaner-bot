namespace LimparEmail.Domain.Exceptions
{
    [Serializable]
    public class ValidationException : Exception
    {
        public ValidationException() : base("Validation error occurred.") { }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
