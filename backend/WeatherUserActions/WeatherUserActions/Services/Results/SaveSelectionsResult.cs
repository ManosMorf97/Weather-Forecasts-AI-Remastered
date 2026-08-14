namespace WeatherUserActions.Services.Results
{
    public enum SaveSelectionsStatus
    {
        Unauthorized,
        InvalidServiceIds,
        Failed,
        Success,
    }

    public readonly record struct SaveSelectionsResult(SaveSelectionsStatus Status)
    {
        public static SaveSelectionsResult Unauthorized() => new(SaveSelectionsStatus.Unauthorized);

        public static SaveSelectionsResult InvalidServiceIds() => new(SaveSelectionsStatus.InvalidServiceIds);

        public static SaveSelectionsResult Failed() => new(SaveSelectionsStatus.Failed);

        public static SaveSelectionsResult Success() => new(SaveSelectionsStatus.Success);
    }
}
