﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EVEMon.Common.Constants;
using EVEMon.Common.Controls;
using EVEMon.Common.Enumerations;
using EVEMon.Common.Models;
using EVEMon.Common.SettingsObjects;

namespace EVEMon.Common.Helpers
{
    /// <summary>
    /// Saves a couple of repetitive tasks.
    /// </summary>
    public static class UIHelper
    {
        public static Bitmap CharacterMonitorScreenshot { get; set; }

        /// <summary>
        /// Saves the plans to a file.
        /// </summary>
        /// <param name="plans">The plans.</param>
        public static void SavePlans(IEnumerable<Plan> plans)
        {
            Character character = (Character)plans.First().Character;

            // Prompt the user to pick a file name
            using (SaveFileDialog sfdSave = new SaveFileDialog())
            {
                sfdSave.FileName = String.Format(CultureConstants.DefaultCulture, "{0} - Plans Backup", character.Name);
                sfdSave.Title = "Save to File";
                sfdSave.Filter = "EVEMon Plans Backup Format (*.epb)|*.epb";
                sfdSave.FilterIndex = (int)PlanFormat.Emp;

                if (sfdSave.ShowDialog() == DialogResult.Cancel)
                    return;

                try
                {
                    string content = PlanIOHelper.ExportAsXML(plans);

                    // Moves to the final file
                    FileHelper.OverwriteOrWarnTheUser(
                        sfdSave.FileName,
                        fs =>
                            {
                                // Emp is actually compressed xml
                                Stream stream = new GZipStream(fs, CompressionMode.Compress);
                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.Write(content);
                                    writer.Flush();
                                    stream.Flush();
                                    fs.Flush();
                                }
                                return true;
                            });
                }
                catch (IOException err)
                {
                    ExceptionHandler.LogException(err, false);
                    MessageBox.Show("There was an error writing out the file:\n\n" + err.Message,
                                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Displays the plan exportation window and then exports it.
        /// </summary>
        /// <param name="plan"></param>
        public static void ExportPlan(Plan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");
            
            ExportPlan(plan, (Character)plan.Character);
        }

        /// <summary>
        /// Exports the character's selected skills as plan.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="selectedSkills">The selected skills.</param>
        public static void ExportCharacterSkillsAsPlan(Character character, IEnumerable<Skill> selectedSkills = null)
        {
            if (character == null)
                throw new ArgumentNullException("character");

            // Create a character without any skill
            CharacterScratchpad scratchpad = new CharacterScratchpad(character);
            scratchpad.ClearSkills();

            // Create a new plan
            Plan plan = new Plan(scratchpad);
            plan.Name = "Skills Plan";

            IEnumerable<Skill> skills = selectedSkills ?? character.Skills.Where(skill => skill.IsPublic);

            // Add all trained skill levels that the character has trained so far
            foreach (Skill skill in skills)
            {
                plan.PlanTo(skill, skill.Level);
            }

            ExportPlan(plan, character);
        }

        /// <summary>
        /// Displays the plan exportation window and then exports it.
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="character"></param>
        private static void ExportPlan(Plan plan, Character character)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            // Assemble an initial filename and remove prohibited characters
            string planSaveName = String.Format(CultureConstants.DefaultCulture, "{0} - {1}", character.Name, plan.Name);
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            int fileInd = planSaveName.IndexOfAny(invalidFileChars);
            while (fileInd != -1)
            {
                planSaveName = planSaveName.Replace(planSaveName[fileInd], '-');
                fileInd = planSaveName.IndexOfAny(invalidFileChars);
            }

            // Prompt the user to pick a file name
            using (SaveFileDialog sfdSave = new SaveFileDialog())
            {
                sfdSave.FileName = planSaveName;
                sfdSave.Title = "Save to File";
                sfdSave.Filter =
                    "EVEMon Plan Format (*.emp)|*.emp|XML  Format (*.xml)|*.xml|Text Format (*.txt)|*.txt";
                sfdSave.FilterIndex = (int)PlanFormat.Emp;

                if (sfdSave.ShowDialog() == DialogResult.Cancel)
                    return;

                // Serialize
                try
                {
                    PlanFormat format = (PlanFormat)sfdSave.FilterIndex;

                    string content;
                    switch (format)
                    {
                        case PlanFormat.Emp:
                        case PlanFormat.Xml:
                            content = PlanIOHelper.ExportAsXML(plan);
                            break;
                        case PlanFormat.Text:
                            // Prompts the user and returns if canceled
                            PlanExportSettings settings = PromptUserForPlanExportSettings(plan);
                            if (settings == null)
                                return;

                            content = PlanIOHelper.ExportAsText(plan, settings);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    // Moves to the final file
                    FileHelper.OverwriteOrWarnTheUser(
                        sfdSave.FileName,
                        fs =>
                            {
                                Stream stream = fs;
                                // Emp is actually compressed text
                                if (format == PlanFormat.Emp)
                                    stream = new GZipStream(fs, CompressionMode.Compress);

                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.Write(content);
                                    writer.Flush();
                                    stream.Flush();
                                    fs.Flush();
                                }
                                return true;
                            });
                }
                catch (IOException err)
                {
                    ExceptionHandler.LogException(err, true);
                    MessageBox.Show("There was an error writing out the file:\n\n" + err.Message,
                                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Prompt the user to select plan exportation settings.
        /// </summary>
        /// <returns></returns>
        public static PlanExportSettings PromptUserForPlanExportSettings(Plan plan)
        {
            PlanExportSettings settings = Settings.Exportation.PlanToText;
            using (CopySaveOptionsWindow f = new CopySaveOptionsWindow(settings, plan, false))
            {
                if (settings.Markup == MarkupType.Undefined)
                    settings.Markup = MarkupType.None;

                f.ShowDialog();
                if (f.DialogResult == DialogResult.Cancel)
                    return null;

                // Save the new settings
                if (!f.SetAsDefault)
                    return settings;

                Settings.Exportation.PlanToText = settings;
                Settings.Save();

                return settings;
            }
        }

        /// <summary>
        /// Displays the character exportation window and then exports it.
        /// Optionally it exports it as it would be after the plan finish.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="plan">The plan.</param>
        public static void ExportCharacter(Character character, Plan plan = null)
        {
            if (character == null)
                throw new ArgumentNullException("character");

            bool isAfterPlanExport = plan != null;

            // Open the dialog box
            using (SaveFileDialog characterSaveDialog = new SaveFileDialog())
            {
                characterSaveDialog.Title = String.Format(CultureConstants.InvariantCulture, "Save {0}Character Info",
                                                          isAfterPlanExport ? "After Plan " : String.Empty);
                characterSaveDialog.Filter =
                    "Text Format|*.txt|CHR Format (EFT)|*.chr|HTML Format|*.html|XML Format (EVEMon)|*.xml";

                if (!isAfterPlanExport)
                    characterSaveDialog.Filter += "|XML Format (CCP API)|*.xml|PNG Image|*.png";

                characterSaveDialog.FileName = String.Format(CultureConstants.InvariantCulture, "{0}{1}",
                                                             character.Name,
                                                             isAfterPlanExport
                                                                 ? String.Format(CultureConstants.InvariantCulture,
                                                                                 " (after plan {0})", plan.Name)
                                                                 : String.Empty);

                characterSaveDialog.FilterIndex = isAfterPlanExport
                                                      ? (int)CharacterSaveFormat.EVEMonXML
                                                      : (int)CharacterSaveFormat.CCPXML;

                if (characterSaveDialog.ShowDialog() == DialogResult.Cancel)
                    return;

                // Serialize
                try
                {
                    CharacterSaveFormat format = (CharacterSaveFormat)characterSaveDialog.FilterIndex;

                    // Save character with the chosen format to our file
                    FileHelper.OverwriteOrWarnTheUser(
                        characterSaveDialog.FileName,
                        fs =>
                            {
                                if (format == CharacterSaveFormat.PNG)
                                {
                                    Bitmap bmp = CharacterMonitorScreenshot; // monitor.GetCharacterScreenshot();
                                    bmp.Save(fs, ImageFormat.Png);
                                    return true;
                                }

                                string content = CharacterExporter.Export(format, character, plan);
                                if ((format == CharacterSaveFormat.CCPXML) && string.IsNullOrEmpty(content))
                                {
                                    MessageBox.Show(
                                        "This character has never been downloaded from CCP, cannot find it in the XML cache.",
                                        "Cannot export the character", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    return false;
                                }

                                using (StreamWriter sw = new StreamWriter(fs))
                                {
                                    sw.Write(content);
                                    sw.Flush();
                                    fs.Flush();
                                }
                                return true;
                            });
                }
                    // Handle exception
                catch (IOException exc)
                {
                    ExceptionHandler.LogException(exc, true);
                    MessageBox.Show("A problem occurred during exportation. The operation has not been completed.");
                }
            }
        }

        /// <summary>
        /// Adds the plans as toolstrip items to the list.
        /// </summary>
        /// <param name="plans">The plans.</param>
        /// <param name="list">The list.</param>
        /// <param name="initialize">The initialize.</param>
        public static void AddTo(this IEnumerable<Plan> plans, ToolStripItemCollection list,
                                 Action<ToolStripMenuItem, Plan> initialize)
        {
            if (plans == null)
                throw new ArgumentNullException("plans");

            if (list == null)
                throw new ArgumentNullException("list");

            if (initialize == null)
                throw new ArgumentNullException("initialize");

            //Scroll through plans
            foreach (Plan plan in plans)
            {
                ToolStripMenuItem item;
                using (ToolStripMenuItem planItem = new ToolStripMenuItem(plan.Name))
                {
                    initialize(planItem, plan);
                    item = planItem;
                }
                list.Add(item);
            }
        }

        /// <summary>
        /// Shows a no support message.
        /// </summary>
        /// <returns></returns>
        internal static object ShowNoSupportMessage()
        {
            MessageBox.Show("The file is probably from an EVEMon version prior to 1.3.0.\n" +
                            "This type of file is no longer supported.",
                            "File type not supported", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return null;
        }
    }
}