/*
    Partial KeePass Database Export Plugin
    Copyright (C) 2017  Heinrich Ulbricht

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using KeePass.Forms;
using KeePass.Plugins;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace PartialKeePassDatabaseExport
{
    public sealed class PartialKeePassDatabaseExportExt : Plugin
    {
        private IPluginHost m_host = null;

        public override bool Initialize(IPluginHost host)
        {
            Terminate();

            m_host = host;
            if (m_host == null) return false;

            m_host.MainWindow.FileSaved += OnFileSaved;

            return true;
        }

        public override void Terminate()
        {
            if (m_host == null) return;

            m_host.MainWindow.FileSaved -= OnFileSaved;

            m_host = null;
        }

        private void OnFileSaved(object sender, FileSavedEventArgs e)
        {
            if (e == null) { Debug.Assert(false); return; }
            if (!e.Success) return;

            try
            {
                ExportDatabasePartial(e.Database);
            }
            catch (Exception ex)
            {
                MessageService.ShowWarning(ex.Message);
            }
        }

        private class ExportConfiguration
        {
            public ProtectedString Password { get; set; }
            public List<string> TagsToExportEntriesFor { get; set; }
            public bool ClearTotpStringEntry { get; set; }
            public string ExportFilePath { get; set; }
        }

        private void ExportDatabasePartial(PwDatabase pd)
        {
            if (pd == null) { Debug.Assert(false); return; }
            if (!pd.IsOpen) { Debug.Assert(false); return; }

            MainForm mf = m_host.MainWindow;
            mf.UIBlockInteraction(true);

            IStatusLogger sl = mf.CreateShowWarningsLogger();
            if (sl != null)
                sl.StartLogging("Saving partial database...", true);

            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                var results = new PwObjectList<PwEntry>();
                // search for our configuration entry
                pd.RootGroup.SearchEntries(new SearchParameters() { SearchString = "^" + Constants.ConfigPwEntryTitle + "$", SearchInTitles = true, RegularExpression = true }, results);
                if (results.UCount != 1)
                {
                    return;
                }
                var configPwEntry = results.GetAt(0);
                if (configPwEntry.Tags.Count == 0)
                    return;

                try
                {
                    var currentDatabasePath = pd.IOConnectionInfo?.Path;
                    if (string.IsNullOrEmpty(currentDatabasePath))
                    {
                        throw new InvalidDataException("Partial Database Export Plugin error: the current database path is empty. This should never happen.");
                    }
                    
                    // user configured relative export path should be resolved relative to the current database path
                    Directory.SetCurrentDirectory(UrlUtil.GetFileDirectory(currentDatabasePath, false, false));

                    // check if the user wants to export to certain path
                    var userConfiguredPath = configPwEntry.Strings.Get(PwDefs.UrlField).ReadString();
                    var absoluteExportFilePath = UrlUtil.StripExtension(currentDatabasePath) + Constants.ExportFileExtension;
                    if (!string.IsNullOrEmpty(userConfiguredPath))
                    {
                        if (string.IsNullOrEmpty(Path.GetFileName(userConfiguredPath)))
                        {
                            // this can happen with trailing slash, e.g. C:\Temp\ => file name is empty; we don't try to be smart but fail
                            throw new FileNotFoundException("Partial Database Export Plugin error: The file name is empty.");
                        }

                        absoluteExportFilePath = Path.GetFullPath(userConfiguredPath);
                        // append file extension if missing or using the wrong one
                        if (!Path.GetExtension(absoluteExportFilePath).Equals(".kdbx", StringComparison.OrdinalIgnoreCase))
                        {
                            absoluteExportFilePath += ".kdbx";
                        }
                        // now we have an absolute path to a new database file
                    }
                    // prevent overwriting the current database...
                    if (absoluteExportFilePath.Equals(Path.GetFullPath(currentDatabasePath), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException("Partial Database Export Plugin error: You specified the current database file as the destination for partial export. This is not supported nor wise.");
                    }

                    var exportSettings = new ExportConfiguration()
                    {
                        Password = configPwEntry.Strings.Get(PwDefs.PasswordField),
                        TagsToExportEntriesFor = configPwEntry.Tags,
                        ClearTotpStringEntry = configPwEntry.Strings.Exists(Constants.ConfigPwStringName_ClearTotp),
                        ExportFilePath = absoluteExportFilePath
                    };
                    ExportToNewDatabase(pd, sl, exportSettings);
                }
                catch (Exception e)
                {
                    // hard error message - tell the user that something is wrong, although not pretty
                    MessageService.ShowFatal("Error while exporting partial database: " + e.ToString());
                }
            }
            finally
            {
                // restore original current directory
                Directory.SetCurrentDirectory(currentDirectory);
                if (sl != null) sl.EndLogging();
                mf.SetStatusEx(null);

                mf.UIBlockInteraction(false);
            }
        }

        private void ExportToNewDatabase(PwDatabase pdOriginal, IStatusLogger slLogger, ExportConfiguration exportConfig)
        {
            var key = new CompositeKey();
            key.AddUserKey(new KcpPassword(exportConfig.Password.ReadString()));

            PwDatabase pd = new PwDatabase();
            pd.New(new IOConnectionInfo(), key);

            // copy settings
            pd.Color = pdOriginal.Color;
            pd.Compression = pdOriginal.Compression;
            pd.DataCipherUuid = pdOriginal.DataCipherUuid;
            pd.DefaultUserName = pdOriginal.DefaultUserName;
            pd.Description = pdOriginal.Description;
            pd.HistoryMaxItems = pdOriginal.HistoryMaxItems;
            pd.HistoryMaxSize = pdOriginal.HistoryMaxSize;
            pd.MaintenanceHistoryDays = pdOriginal.MaintenanceHistoryDays;
            pd.MasterKeyChangeForce = pdOriginal.MasterKeyChangeForce;
            pd.MasterKeyChangeRec = pdOriginal.MasterKeyChangeRec;
            pd.Name = pdOriginal.Name;
            pd.RecycleBinEnabled = pdOriginal.RecycleBinEnabled;

            pd.RootGroup.Name = pdOriginal.RootGroup.Name;
            pdOriginal.RootGroup.TraverseTree(
                TraversalMethod.PreOrder,
                null,
                pwEntry =>
                {
                    // copy custom icons
                    if (!pwEntry.CustomIconUuid.Equals(PwUuid.Zero))
                    {
                        pd.CustomIcons.AddRange(pdOriginal.CustomIcons.FindAll(icon => icon.Uuid.Equals(pwEntry.CustomIconUuid)));
                    }

                    // check if entry has any of the tags to copy entries for
                    var foundTag = false;
                    foreach (var tag in pwEntry.Tags)
                    {
                        if (exportConfig.TagsToExportEntriesFor.Contains(tag))
                        {
                            foundTag = true;
                            break;
                        }
                    }

                    // found it (and it's not the config entry) -> clone
                    if (foundTag && pwEntry.Strings.Get(PwDefs.TitleField).ReadString() != Constants.ConfigPwEntryTitle)
                    {
                        var pwEntryClone = pwEntry.CloneDeep();
                        if (exportConfig.ClearTotpStringEntry)
                        {
                            // remove TOTP secret key (when using the https://keepass.info/plugins.html#keeotp plugin)
                            var keyExists = pwEntryClone.Strings.Exists(Constants.ConfigPwStringName_Otp);
                            if (keyExists)
                            {
                                pwEntryClone.Strings.Remove(Constants.ConfigPwStringName_Otp);
                                var title = pwEntryClone.Strings.Get(PwDefs.TitleField);
                                // add hint that 2FA key has been removed, but only if title is not empty (that's fishy)
                                if (!string.IsNullOrEmpty(title.ReadString()))
                                {
                                    pwEntryClone.Strings.Set(PwDefs.TitleField, new ProtectedString(title.IsProtected, title.ReadString() + " [2FA removed]"));
                                }
                            }
                        }
                        pd.RootGroup.AddEntry(pwEntryClone, true);
                    }

                    return true;
                }
            );

            IOConnectionInfo iocOrig = pdOriginal.IOConnectionInfo;
            if (pdOriginal == null) { Debug.Assert(false); }

            IOConnectionInfo iocNew = iocOrig.CloneDeep();
            if (string.IsNullOrEmpty(iocNew.Path)) { Debug.Assert(false); }

            iocNew.Path = exportConfig.ExportFilePath;
            pd.SaveAs(iocNew, false, slLogger);
        }
    }
}
