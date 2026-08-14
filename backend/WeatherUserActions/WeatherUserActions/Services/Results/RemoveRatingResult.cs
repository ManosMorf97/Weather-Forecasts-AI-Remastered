namespace WeatherUserActions.Services.Results
{
    public enum RemoveRatingStatus
    {
        Unauthorized,
        Failed,
        Success,
    }

    public readonly record struct RemoveRatingResult(RemoveRatingStatus Status)
    {
        public static RemoveRatingResult Unauthorized() => new(RemoveRatingStatus.Unauthorized);

        public static RemoveRatingResult Failed() => new(RemoveRatingStatus.Failed);

        public static RemoveRatingResult Success() => new(RemoveRatingStatus.Success);
    }
}
