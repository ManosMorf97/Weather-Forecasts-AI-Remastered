namespace WeatherUserActions.Services
{
    public enum SaveServicesStatus
    {
        Unauthorized,
        InvalidServiceIds,
        Failed,
        Success,
    }

    public readonly record struct SaveServicesResult(SaveServicesStatus Status)
    {
        public static SaveServicesResult Unauthorized() => new(SaveServicesStatus.Unauthorized);

        public static SaveServicesResult InvalidServiceIds() => new(SaveServicesStatus.InvalidServiceIds);

        public static SaveServicesResult Failed() => new(SaveServicesStatus.Failed);

        public static SaveServicesResult Success() => new(SaveServicesStatus.Success);
    }
}
