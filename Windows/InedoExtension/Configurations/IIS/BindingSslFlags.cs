using System;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Flags]
    public enum BindingSslFlags
    {
        None = 0,
        ServerNameIndication = 1,
        UseCentralizedStore = 2
    }

}
