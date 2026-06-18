namespace ScraperAPI.Services.VerificationService
{
    public interface IVerificationService
    {
        public Task<string?> GenerateVerificationCodeAsync(string email);
        public Task<bool?> SendVerificationCodeAsync(string email);
        public Task<bool?> CheckVerificationCodeAsync(string email, string code);
    }
}  