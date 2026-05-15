namespace FixIt.Mobile.Services.Contracts;

public interface IPerformanceService
{
    IDisposable StartTrace(string name);
}
