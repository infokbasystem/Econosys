using Microsoft.AspNetCore.DataProtection;

namespace Econosys.Api.Services
{
    public interface IProtectedSecretService
    {
        string Protect(string purpose, string value);
        string Unprotect(string purpose, string value);
    }

    public class ProtectedSecretService : IProtectedSecretService
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public ProtectedSecretService(IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtectionProvider = dataProtectionProvider;
        }

        public string Protect(string purpose, string value)
        {
            return _dataProtectionProvider.CreateProtector(purpose).Protect(value);
        }

        public string Unprotect(string purpose, string value)
        {
            return _dataProtectionProvider.CreateProtector(purpose).Unprotect(value);
        }
    }
}
