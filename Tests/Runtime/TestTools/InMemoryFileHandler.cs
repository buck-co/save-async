using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// A FileHandler used for testing which does not write anything to disk, but instead
    /// stores data in memory. Can be configured to simulate slow operations.
    /// </summary>
    public class InMemoryFileHandler : FileHandler
    {
        public TimeSpan AllOperationDelay { get; set; } = TimeSpan.Zero;

        readonly Dictionary<string, string> m_files = new();
        
        protected override string GetPath(string pathOrFilename) => pathOrFilename;

        public override async Task<bool> Exists(string pathOrFilename, CancellationToken cancellationToken)
        {
            await Task.Delay(AllOperationDelay, cancellationToken);
            return m_files.ContainsKey(pathOrFilename);
        }
        
        public override async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
        {
            await Task.Delay(AllOperationDelay, cancellationToken);
            m_files[pathOrFilename] = content;
        }
        
        public override async Task<string> ReadFile(string pathOrFilename, CancellationToken cancellationToken)
        {
            await Task.Delay(AllOperationDelay, cancellationToken);
            return m_files[pathOrFilename] ?? "";
        }

        public override async Task Erase(string pathOrFilename, CancellationToken cancellationToken)
        {
            await Task.Delay(AllOperationDelay, cancellationToken);
            m_files[pathOrFilename] = "";
        }
        
        public override void Delete(string pathOrFilename)
        {
            // Delete is sync, no delay
            m_files.Remove(pathOrFilename);
        }
    }
}