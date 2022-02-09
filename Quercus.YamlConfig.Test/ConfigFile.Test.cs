using System.Threading;
using System.Text;
using System.IO;
using System;
using NLog;
using NUnit.Framework;
using Moq;
using FluentAssertions;
using System.ComponentModel;

namespace Quercus.YamlConfig
{
    [TestFixture]
    public class ConfigFileTest
    {
        private readonly Logger Log = LogManager.GetCurrentClassLogger();

        private ConfigFile Create(IConfigStorage config = null)
        {
            return new ConfigFile(config ?? Mock.Of<IConfigStorage>());
        }

        [Test]
        public void _01_FilePath_is_null_by_default()
        {
            var file = Create();

            file.Path.Should().BeNull();
        }

        [Test]
        public void _02_FilePath_can_be_assigned()
        {
            var file = Create();

            var path = "Any text here!!!";
            file.Path = path;
            file.Path.Should().Be(path);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("inv>alid")]
        public void _03_Trying_to_load_with_incorrect_path_fires_InvalidOperationException(string path)
        {
            var file = Create();

            file.Path = path;
            file.Invoking(_ => _.Load())
                .Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void _04_If_file_at_Path_doesnot_exist_then_Load_doesnot_actually_Load_anything()
        {
            var configMock = new Mock<IConfigStorage>();
            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            File.Delete(file.Path); // deleting file

            file.Load();
            configMock.Verify(_ => _.Load(It.IsAny<TextReader>()), Times.Never);
        }

        [Test]
        public void _05_If_file_at_Path_exists_then_its_opened_and_passed_to_Load_in_IConfig()
        {
            var configMock = new Mock<IConfigStorage>();
            var readData = "";
            configMock.Setup(x => x.Load(It.IsAny<TextReader>()))
                .Callback<TextReader>(r => readData = r.ReadToEnd());
            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                var fileData = "Hello everyone!!! This is just a sample.";
                File.AppendAllText(file.Path, fileData, Encoding.UTF8);

                file.Load();
                configMock.Verify(x => x.Load(It.IsAny<TextReader>()), Times.Once);
                readData.Should().Be(fileData);
            }
            finally
            {
                File.Delete(file.Path);
            }
        }

        [Test]
        public void _06_If_any_error_occurs_while_loading_file_it_is_thrown_up()
        {
            var configMock = new Mock<IConfigStorage>();
            configMock.Setup(x => x.Load(It.IsAny<TextReader>()))
                .Throws(new FormatException());
            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            var fileData = "Hello everyone!!! This is just a sample.";
            File.AppendAllText(file.Path, fileData, Encoding.UTF8);

            file.Invoking(_ => _.Load())
                .Should().Throw<FormatException>();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("inv>alid")]
        public void _07_Trying_to_save_with_incorrect_path_fires_InvalidOperationException(string path)
        {
            var file = Create();

            file.Path = path;
            file.Invoking(_ => _.Save())
                .Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void _08_Saving_calls_Save_in_contained_IConfig()
        {
            var configMock = new Mock<IConfigStorage>();
            var configText = "Just a sample";
            configMock.Setup(_ => _.Save(It.IsAny<TextWriter>()))
                .Callback<TextWriter>(w => w.Write(configText));

            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                file.Save();

                configMock.Verify(_ => _.Save(It.IsAny<TextWriter>()), Times.Once);
                File.ReadAllText(file.Path, Encoding.UTF8).Should().Be(configText);
            }
            finally
            {
                File.Delete(file.Path);
            }
        }

        [Test]
        public void _09_If_any_error_occurs_during_Save_it_is_thrown_up()
        {
            var configMock = new Mock<IConfigStorage>();
            configMock.Setup(_ => _.Save(It.IsAny<TextWriter>()))
                .Throws(new FormatException());

            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                file.Invoking(_ => _.Save())
                    .Should().Throw<FormatException>();
            }
            finally
            {
                File.Delete(file.Path);
            }
        }

        [Test]
        public void _10_By_default_AutoSave_is_false()
        {
            var file = Create();

            file.AutoSave.Should().Be(false);
        }

        [Test]
        public void _11_By_default_AutoSaveDebounceTime_is_1_minute()
        {
            var file = Create();

            file.AutoSaveDebounceTime.Should().Be(TimeSpan.FromMinutes(1));
        }

        [Test]
        public void _12_In_AutoSave_mode_if_config_has_been_changed_and_AutoSaveDebounceTime_passed_then_config_is_saved()
        {
            var configMock = new Mock<IConfigStorage>();
            var saveInvoked = new ManualResetEvent(false);
            configMock.Setup(_ => _.Save(It.IsAny<TextWriter>()))
                .Callback(() => saveInvoked.Set());

            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                file.AutoSaveDebounceTime = TimeSpan.FromMilliseconds(10); // just a minimal time
                file.AutoSave = true;

                configMock.Raise(x => x.ContentChanged += null, EventArgs.Empty);

                saveInvoked.WaitOne(TimeSpan.FromSeconds(1), false).Should().Be(true);
            }
            finally
            {
                DeleteTempFile(file.Path);
            }
        }

        [Test]
        public void _13_In_AutoSave_mode_if_another_change_came_while_debouncing_timer_is_restart()
        {
            var configMock = new Mock<IConfigStorage>();
            var saveInvoked = new ManualResetEvent(false);
            configMock.Setup(_ => _.Save(It.IsAny<TextWriter>()))
                .Callback(() =>
                {
                    saveInvoked.Set();
                });

            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                file.AutoSaveDebounceTime = TimeSpan.FromMilliseconds(100);
                file.AutoSave = true;

                configMock.Raise(x => x.ContentChanged += null, EventArgs.Empty);
                Thread.Sleep(80);

                saveInvoked.WaitOne(0, false).Should().Be(false);
                configMock.Raise(x => x.ContentChanged += null, EventArgs.Empty);
                Thread.Sleep(80);

                saveInvoked.WaitOne(0, false).Should().Be(false);
                saveInvoked.WaitOne(TimeSpan.FromSeconds(1), false).Should().Be(true);
            }
            finally
            {
                DeleteTempFile(file.Path);
            }
        }

        [Test]
        public void _14_In_AutoSave_mode_when_config_was_changed_and_ConfigFile_is_disposed_it_is_saved()
        {
            var configMock = new Mock<IConfigStorage>();
            var saveInvoked = new ManualResetEvent(false);
            configMock.Setup(_ => _.Save(It.IsAny<TextWriter>()))
                .Callback(() =>
                {
                    saveInvoked.Set();
                });

            var file = Create(configMock.Object);

            file.Path = Path.GetTempFileName();
            try
            {
                file.AutoSave = true;

                configMock.Raise(x => x.ContentChanged += null, EventArgs.Empty);
                file.Dispose();

                saveInvoked.WaitOne(TimeSpan.FromSeconds(1), false).Should().Be(true);
            }
            finally
            {
                DeleteTempFile(file.Path);
            }
        }

        /// <summary>
        /// This method makes 10 attempts to delete temp file with 10ms delays.
        /// It is used in case when some other code may lock the file.
        /// </summary>
        private static void DeleteTempFile(string path)
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    File.Delete(path);
                    break; ;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}