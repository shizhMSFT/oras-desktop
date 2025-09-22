using System;

namespace OrasProject.OrasDesktop.Services
{
    public class RegistryOperationException : Exception
    {
        public RegistryOperationException(string message) : base(message)
        {
        }

        public RegistryOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}