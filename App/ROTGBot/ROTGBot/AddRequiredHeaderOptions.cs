namespace ROTGBot
{
    public class AddRequiredHeaderOptions
    {
        public string AuthorizationName { get; set; } = "Authorization";
        public string DefaultDescription { get; set; } = "access token";
        public string StringType { get; set; } = "string";
        public string BearerDefaultApiString { get; set; } = "Bearer ";
    }
}
