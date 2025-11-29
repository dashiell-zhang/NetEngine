using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Repository.Extensions;
public static class DatabaseFacadeExtension
{

    public static ForceMasterScope BeginForceMaster(this DatabaseFacade databaseFacade) => new();

    public class ForceMasterScope : IDisposable
    {
        public ForceMasterScope()
        {
            forceMaster.Value = true;
        }

        public void Dispose()
        {
            forceMaster.Value = false;
        }
    }


    private static AsyncLocal<bool> forceMaster = new();


    public static bool IsUseForceMaster()
    {
        return forceMaster.Value;
    }

}
