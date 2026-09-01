namespace WeatherUserActions.Email
{
    public record EmailAttachment(string FileName, string ContentType, byte[] Content);
}
