namespace Drocsid.HenrikDennis2025.Core.Services;

public static class ServiceLocator
{
    private static readonly Lazy<DrocsidClientService> _drocsidClient = 
        new Lazy<DrocsidClientService>(() => new DrocsidClientService());

    public static DrocsidClientService DrocsidClient => _drocsidClient.Value;
}