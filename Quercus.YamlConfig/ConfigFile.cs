using System.Text;
using System.IO;
using System;
using System.Threading;

namespace Quercus.YamlConfig
{
    /// <summary>
    /// Class, that loads and saves given Config from given path.
    /// Calls Config.Load and Save methods for file in given path.
    /// Can load config upon creation (loadOnCreate flag)
    /// Can save config when any changes are made to it (autoSave flag). To reduce number of actual save 
    /// operations save is postponed for AutoSaveDebounceTime (1 minute by default).
    /// If autoSave flag is set, config is also saved upon disposing (if config was changed).
    /// </summary>
    public class ConfigFile : IConfigFile, IDisposable
    {
        private Timer debounceTimer = null!;
        private bool autoSave;
        private bool changed;

        public ConfigFile(IConfigStorage config)
        {
            Config = config;
            config.ContentChanged += ConfigContentChanged;
        }

        ~ConfigFile()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                Config.ContentChanged -= ConfigContentChanged;
                if (debounceTimer != null)
                {
                    debounceTimer.Dispose();
                    debounceTimer = null!;
                }
            }
            if (changed)
                PerformAutoSave();
            if (disposing)
                GC.SuppressFinalize(this);
        }

        public IConfigStorage Config { get; }

        public string Path { get; set; } = null!;

        public bool AutoSave
        {
            get => autoSave;
            set
            {
                if (autoSave == value)
                    return;
                autoSave = value;
                lock (this)
                {
                    if (autoSave)
                        debounceTimer = new Timer(_ => PerformAutoSave(), null, Timeout.Infinite, Timeout.Infinite);
                    else
                    {
                        debounceTimer.Dispose();
                        debounceTimer = null!;
                    }
                }
            }
        }

        public TimeSpan AutoSaveDebounceTime { get; set; } = TimeSpan.FromMinutes(1);

        public void Load()
        {
            if (string.IsNullOrWhiteSpace(Path))
                throw new InvalidOperationException($"Path '{Path}' is not valid configuration file path");
            if (Path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
                throw new InvalidOperationException($"Path '{Path}' is not valid configuration file path");
            try
            {
                using (var reader = new StreamReader(File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8))
                    Config.Load(reader);
            }
            catch (FileNotFoundException)
            {
                // ignoring - using empty config file
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Path))
                throw new InvalidOperationException($"Path '{Path}' is not valid configuration file path");
            if (Path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
                throw new InvalidOperationException($"Path '{Path}' is not valid configuration file path");
            if (debounceTimer != null)
                debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            using (var writer = new StreamWriter(File.Create(Path), Encoding.UTF8))
                Config.Save(writer);
            changed = false;
            Saved?.Invoke(this, EventArgs.Empty);
        }

        private void PerformAutoSave()
        {
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                AutoSaveFailed?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
            }
        }

        private void ConfigContentChanged(object sender, EventArgs e)
        {
            lock (this)
            {
                if (debounceTimer == null)
                    return;
                changed = true;
                debounceTimer.Change(AutoSaveDebounceTime, Timeout.InfiniteTimeSpan);
            }
        }

        public event EventHandler Saved = null!;

        public event UnhandledExceptionEventHandler AutoSaveFailed = null!;
    }
}