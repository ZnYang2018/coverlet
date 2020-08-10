using Coverlet.Core.Abstractions;
using Coverlet.Core.Helpers;

namespace Coverlet.Console.Custom
{
    class CustomInstrumentationHelper : InstrumentationHelper, IInstrumentationHelper
    {
        public CustomInstrumentationHelper(IProcessExitHandler processExitHandler, IRetryHelper retryHelper, IFileSystem fileSystem, ILogger logger, ISourceRootTranslator sourceRootTranslator)
            : base(processExitHandler, retryHelper, fileSystem, logger, sourceRootTranslator)
        {
        }

        public override void RestoreOriginalModule(string module, string identifier)
        {
            // DO NOT RESTORE
        }

        public override void RestoreOriginalModules()
        {
            // DO NOT RESTORE
        }

        public new void DeleteHitsFile(string path)
        {
            // Don't delete hits file
        }
    }
}
