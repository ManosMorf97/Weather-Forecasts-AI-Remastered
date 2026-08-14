namespace WeatherUserActions.Services
{
    public enum ProfileCreationStatus
    {
        Unauthorized,
        ProvisioningFailed,
        SelectionCheckFailed,
        Success,
    }

    public readonly record struct ProfileCreationResult(ProfileCreationStatus Status, bool HasCitySiteSelection = false)
    {
        public static ProfileCreationResult Unauthorized() => new(ProfileCreationStatus.Unauthorized);

        public static ProfileCreationResult ProvisioningFailed() => new(ProfileCreationStatus.ProvisioningFailed);

        public static ProfileCreationResult SelectionCheckFailed() => new(ProfileCreationStatus.SelectionCheckFailed);

        public static ProfileCreationResult Success(bool hasCitySiteSelection) =>
            new(ProfileCreationStatus.Success, hasCitySiteSelection);
    }
}
