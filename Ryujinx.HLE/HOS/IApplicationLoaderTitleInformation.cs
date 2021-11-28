using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS
{
    public interface IApplicationLoaderTitleInformation
    {
        ulong TitleId { get; }
        bool TitleIs64Bit { get; }
        string TitleIdText { get; }
        string DisplayVersion { get; }
        string TitleName { get; }
    }
}
