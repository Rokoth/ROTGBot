namespace ROTGBot
{
    public static class AddRequiredHeaderOptions
    {
        public static string AuthorizationName { get; set; } = "Authorization";
        public static string DefaultDescription { get; set; } = "access token";
        public static string StringType { get; set; } = "string";
        public static string BearerDefaultApiString { get; set; } = "Bearer ";
    }
}
