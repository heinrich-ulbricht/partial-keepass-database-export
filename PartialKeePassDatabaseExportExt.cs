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

        private void ExportDatabasePartial(PwDatabase pd)
        {
            if (pd == null) { Debug.Assert(false); return; }
            if (!pd.IsOpen) { Debug.Assert(false); return; }

            MainForm mf = m_host.MainWindow;
            mf.UIBlockInteraction(true);

            IStatusLogger sl = mf.CreateShowWarningsLogger();
            if (sl != null)
                sl.StartLogging("Saving partial database...", true);

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
                    ExportToNewDatabase(pd, sl, configPwEntry.Strings.Get(PwDefs.PasswordField), configPwEntry.Tags, configPwEntry.Strings.Exists(Constants.ConfigPwStringName_ClearTotp));
                }
                catch (Exception e)
                {
                    // hard error message - tell the user that something is wrong, although not pretty
                    MessageService.ShowFatal("Error while exporting partial database: " + e.ToString());
                }
            }
            finally
            {
                if (sl != null) sl.EndLogging();
                mf.SetStatusEx(null);

                mf.UIBlockInteraction(false);
            }
        }

        public void ExportToNewDatabase(PwDatabase pdOriginal, IStatusLogger slLogger, ProtectedString passwordOfNewDb, List<string> tagsToExportEntriesFor, bool clearTotpKey)
        {
            var key = new CompositeKey();
            key.AddUserKey(new KcpPassword(passwordOfNewDb.ReadString()));

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
                        if (tagsToExportEntriesFor.Contains(tag))
                        {
                            foundTag = true;
                            break;
                        }
                    }

                    // found it (and it's not the config entry) -> clone
                    if (foundTag && pwEntry.Strings.Get(PwDefs.TitleField).ReadString() != Constants.ConfigPwEntryTitle)
                    {
                        var pwEntryClone = pwEntry.CloneDeep();
                        if (clearTotpKey)
                        {
                            // remove TOTP secret key (when using the https://keepass.info/plugins.html#keeotp plugin)
                            pwEntryClone.Strings.Remove(Constants.ConfigPwStringName_Otp);
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

            iocNew.Path = UrlUtil.StripExtension(iocNew.Path) + Constants.ExportFileExtension;
            pd.SaveAs(iocNew, false, slLogger);
        }
    }
}
