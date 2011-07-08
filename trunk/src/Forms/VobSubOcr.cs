﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.OCR;
using Nikse.SubtitleEdit.Logic.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.VobSub;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class VobSubOcr : Form
    {
        internal class CompareItem
        {
            public Bitmap Bitmap { get; private set; }
            public string Name { get; private set; }
            public bool Italic { get; set; }
            public int ExpandCount { get; private set; }

            public CompareItem(Bitmap bmp, string name, bool isItalic, int expandCount)
            {
                Bitmap = bmp;
                Name = name;
                Italic = isItalic;
                ExpandCount = expandCount;
            }
        }

        internal class CompareMatch
        {
            public string Text { get; set; }
            public bool Italic { get; set; }
            public int ExpandCount { get; set; }
            public string Name { get; set; }
            public CompareMatch(string text, bool italic, int expandCount, string name)
            {
                Text = text;
                Italic = italic;
                ExpandCount = expandCount;
                Name = name;
            }
        }

        internal class ImageCompareAddition
        {
            public string Name { get; set; }
            public string Text { get; set; }
            public Bitmap Image { get; set; }
            public bool Italic { get; set; }
            public int Index { get; set; }

            public ImageCompareAddition(string name, string text, Bitmap image, bool italic, int index)
            {
                Name = name;
                Text = text;
                Image = image;
                Text = text;
                Italic = italic;
                Index = index;
            }
        }

        private class TesseractLanguage
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public override string ToString()
            {
                return Text;
            }
        }

        private class ModiParameter
        {
            public Bitmap Bitmap { get; set; }
            public string Text { get; set; }
            public int Language { get; set; }
        }

        public string FileName { get; set; }
        Subtitle _subtitle = new Subtitle();
        List<CompareItem> _compareBitmaps;
        XmlDocument _compareDoc = new XmlDocument();
        Point _manualOcrDialogPosition = new Point(-1, -1);
        volatile bool _abort;
        int _selectedIndex = -1;
        VobSubOcrSettings _vobSubOcrSettings;
        bool _italicCheckedLast;
        bool _useNewSubIdxCode;

        Type _modiType; 
        Object _modiDoc;
        bool _modiEnabled;

        // DVD rip/vobsub
        List<VobSubMergedPack> _vobSubMergedPackistOriginal;
        List<VobSubMergedPack> _vobSubMergedPackist;
        List<Color> _palette;

        // BluRay sup
        List<Logic.BluRaySup.BluRaySupPicture> _bluRaySubtitlesOriginal;
        List<Logic.BluRaySup.BluRaySupPicture> _bluRaySubtitles;
        Nikse.SubtitleEdit.Logic.BluRaySup.BluRaySupPalette _defaultPaletteInfo;

        string _lastLine;
        string _languageId;

        // Dictionaries/spellchecking/fixing
        OcrFixEngine _ocrFixEngine;
        int _tessnetOcrAutoFixes;

        Subtitle _bdnXmlOriginal;
        Subtitle _bdnXmlSubtitle;
        string _bdnFileName;

        List<ImageCompareAddition> _lastAdditions = new List<ImageCompareAddition>();
        VobSubOcrCharacter _vobSubOcrCharacter = new VobSubOcrCharacter();

        public VobSubOcr()
        {
            InitializeComponent();

            var language = Configuration.Settings.Language.VobSubOcr;
            Text = language.Title;
            groupBoxOcrMethod.Text = language.OcrMethod;
            labelTesseractLanguage.Text = language.Language;
            labelImageDatabase.Text = language.ImageDatabase;
            labelNoOfPixelsIsSpace.Text = language.NoOfPixelsIsSpace;
            buttonNewCharacterDatabase.Text = language.New;
            buttonEditCharacterDatabase.Text = language.Edit;
            buttonStartOcr.Text = language.StartOcr;
            buttonStop.Text = language.Stop;
            labelStartFrom.Text = language.StartOcrFrom;
            labelStatus.Text = language.LoadingVobSubImages;
            groupBoxSubtitleImage.Text = language.SubtitleImage;
            labelSubtitleText.Text = language.SubtitleText;
            buttonOK.Text = Configuration.Settings.Language.General.OK;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            subtitleListView1.InitializeLanguage(Configuration.Settings.Language.General, Configuration.Settings);
            subtitleListView1.Columns[0].Width = 45;
            subtitleListView1.Columns[1].Width = 90;
            subtitleListView1.Columns[2].Width = 90;
            subtitleListView1.Columns[3].Width = 70;
            subtitleListView1.Columns[4].Width = 150;
            subtitleListView1.InitializeTimeStampColumWidths(this);

            groupBoxImagePalette.Text = language.ImagePalette;
            checkBoxCustomFourColors.Text = language.UseCustomColors;
            checkBoxBackgroundTransparent.Text = language.Transparent;
            checkBoxPatternTransparent.Text = language.Transparent;
            checkBoxEmphasis1Transparent.Text = language.Transparent;
            checkBoxEmphasis2Transparent.Text = language.Transparent;
            checkBoxAutoTransparentBackground.Text = language.AutoTransparentBackground;
            checkBoxPromptForUnknownWords.Text = language.PromptForUnknownWords;

            groupBoxOcrAutoFix.Text = language.OcrAutoCorrectionSpellchecking;
            checkBoxGuessUnknownWords.Text = language.TryToGuessUnkownWords;
            checkBoxAutoBreakLines.Text = language.AutoBreakSubtitleIfMoreThanTwoLines;
            tabControlLogs.TabPages[0].Text = language.AllFixes;
            tabControlLogs.TabPages[1].Text = language.GuessesUsed;
            tabControlLogs.TabPages[2].Text = language.UnknownWords;

            numericUpDownPixelsIsSpace.Left = labelNoOfPixelsIsSpace.Left + labelNoOfPixelsIsSpace.Width + 5;
            groupBoxSubtitleImage.Text = string.Empty;
            labelFixesMade.Text = string.Empty;
            labelFixesMade.Left = checkBoxAutoFixCommonErrors.Left + checkBoxAutoFixCommonErrors.Width;
            
            labelDictionaryLoaded.Text = string.Format(Configuration.Settings.Language.VobSubOcr.DictionaryX, string.Empty);
            comboBoxDictionaries.Left = labelDictionaryLoaded.Left + labelDictionaryLoaded.Width;

            groupBoxImageCompareMethod.Text = language.OcrViaImageCompare;
            groupBoxModiMethod.Text = language.OcrViaModi;
            checkBoxAutoFixCommonErrors.Text = language.FixOcrErrors;
            checkBoxRightToLeft.Text = language.RightToLeft;
            checkBoxRightToLeft.Left = numericUpDownPixelsIsSpace.Left;
            groupBoxOCRControls.Text = language.StartOcr + " / " + language.Stop;

            comboBoxDictionaries.SelectedIndexChanged -= comboBoxDictionaries_SelectedIndexChanged;
            comboBoxDictionaries.Items.Clear();
            comboBoxDictionaries.Items.Add(Configuration.Settings.Language.General.None);
            foreach (string name in Utilities.GetDictionaryLanguages())
            {
                comboBoxDictionaries.Items.Add(name);
            }
            comboBoxDictionaries.SelectedIndexChanged += comboBoxDictionaries_SelectedIndexChanged;


            comboBoxOcrMethod.Items.Clear();
            comboBoxOcrMethod.Items.Add(language.OcrViaTesseract);
            comboBoxOcrMethod.Items.Add(language.OcrViaImageCompare);
            comboBoxOcrMethod.Items.Add(language.OcrViaModi);

            checkBoxUseModiInTesseractForUnknownWords.Text = language.TryModiForUnknownWords;
            checkBoxShowOnlyForced.Text = language.ShowOnlyForcedSubtitles;
            checkBoxUseTimeCodesFromIdx.Text = language.UseTimeCodesFromIdx;

            normalToolStripMenuItem.Text = Configuration.Settings.Language.Main.Menu.ContextMenu.Normal;
            italicToolStripMenuItem.Text = Configuration.Settings.Language.General.Italic;
            importTextWithMatchingTimeCodesToolStripMenuItem.Text = language.ImportTextWithMatchingTimeCodes;
            saveImageAsToolStripMenuItem.Text = language.SaveSubtitleImageAs;
            saveAllImagesToolStripMenuItem.Text = language.SaveAllSubtitleImagesAsBdnXml;
            saveAllImagesWithHtmlIndexViewToolStripMenuItem.Text = language.SaveAllSubtitleImagesWithHtml;
            inspectImageCompareMatchesForCurrentImageToolStripMenuItem.Text = language.InspectCompareMatchesForCurrentImage;
            EditLastAdditionsToolStripMenuItem.Text = language.EditLastAdditions;
            checkBoxRightToLeft.Checked = Configuration.Settings.VobSubOcr.RightToLeft;

            comboBoxTesseractLanguages.Left = labelTesseractLanguage.Left + labelTesseractLanguage.Width;

            Utilities.InitializeSubtitleFont(subtitleListView1);
            subtitleListView1.AutoSizeAllColumns(this);

            italicToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainListViewItalic);

            comboBoxTesseractLanguages.Left = labelTesseractLanguage.Left + labelTesseractLanguage.Width + 3;
            comboBoxModiLanguage.Left = label1.Left + label1.Width + 3;
            
            comboBoxCharacterDatabase.Left = labelImageDatabase.Left + labelImageDatabase.Width + 3;
            buttonNewCharacterDatabase.Left = comboBoxCharacterDatabase.Left + comboBoxCharacterDatabase.Width + 3;
            buttonEditCharacterDatabase.Left = buttonNewCharacterDatabase.Left;
            numericUpDownPixelsIsSpace.Left = labelNoOfPixelsIsSpace.Left + labelNoOfPixelsIsSpace.Width + 3;
            checkBoxRightToLeft.Left = numericUpDownPixelsIsSpace.Left;

            FixLargeFonts();
            buttonEditCharacterDatabase.Top = buttonNewCharacterDatabase.Top + buttonNewCharacterDatabase.Height + 3;
        }

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonCancel.Text, this.Font);
            if (textSize.Height > buttonCancel.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);
            }
        }

        internal bool Initialize(string vobSubFileName, VobSubOcrSettings vobSubOcrSettings, bool useNewSubIdxCode)
        {
            _useNewSubIdxCode = useNewSubIdxCode;
            buttonOK.Enabled = false;
            buttonCancel.Enabled = false;
            buttonStartOcr.Enabled = false;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = false;
            buttonEditCharacterDatabase.Enabled = false;
            labelStatus.Text = string.Empty;
            progressBar1.Visible = false;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            numericUpDownPixelsIsSpace.Value = vobSubOcrSettings.XOrMorePixelsMakesSpace;
            _vobSubOcrSettings = vobSubOcrSettings;

            InitializeModi();
            InitializeTesseract();
            LoadImageCompareCharacterDatabaseList();

            if (Configuration.Settings.VobSubOcr.LastOcrMethod == "BitmapCompare" && comboBoxOcrMethod.Items.Count > 1)
                comboBoxOcrMethod.SelectedIndex = 1;
            else if (Configuration.Settings.VobSubOcr.LastOcrMethod == "MODI" && comboBoxOcrMethod.Items.Count > 2)
                comboBoxOcrMethod.SelectedIndex = 2;
            else
                comboBoxOcrMethod.SelectedIndex = 0;

            FileName = vobSubFileName;
            Text += " - " + Path.GetFileName(FileName);

            return InitializeSubIdx(vobSubFileName);
        }

        internal void Initialize(List<VobSubMergedPack> vobSubMergedPackist, List<Color> palette, VobSubOcrSettings vobSubOcrSettings, string languageString)
        {
            buttonOK.Enabled = false;
            buttonCancel.Enabled = false;
            buttonStartOcr.Enabled = false;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = false;
            buttonEditCharacterDatabase.Enabled = false;
            labelStatus.Text = string.Empty;
            progressBar1.Visible = false;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            numericUpDownPixelsIsSpace.Value = vobSubOcrSettings.XOrMorePixelsMakesSpace;
            _vobSubOcrSettings = vobSubOcrSettings;

            InitializeModi();
            InitializeTesseract();
            LoadImageCompareCharacterDatabaseList();

            if (Configuration.Settings.VobSubOcr.LastOcrMethod == "BitmapCompare" && comboBoxOcrMethod.Items.Count > 1)
                comboBoxOcrMethod.SelectedIndex = 1;
            else if (Configuration.Settings.VobSubOcr.LastOcrMethod == "MODI" && comboBoxOcrMethod.Items.Count > 2)
                comboBoxOcrMethod.SelectedIndex = 2;
            else
                comboBoxOcrMethod.SelectedIndex = 0;

            _vobSubMergedPackist = vobSubMergedPackist;
            _palette = palette;

            if (_palette == null)
                checkBoxCustomFourColors.Checked = true;

            SetTesseractLanguageFromLanguageString(languageString);
        }

        internal void Initialize(List<Logic.BluRaySup.BluRaySupPicture> subtitles, VobSubOcrSettings vobSubOcrSettings)
        {
            buttonOK.Enabled = false;
            buttonCancel.Enabled = false;
            buttonStartOcr.Enabled = false;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = false;
            buttonEditCharacterDatabase.Enabled = false;
            labelStatus.Text = string.Empty;
            progressBar1.Visible = false;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            numericUpDownPixelsIsSpace.Value = 11; // vobSubOcrSettings.XOrMorePixelsMakesSpace;
            _vobSubOcrSettings = vobSubOcrSettings;

            InitializeModi();
            InitializeTesseract();
            LoadImageCompareCharacterDatabaseList();

            if (Configuration.Settings.VobSubOcr.LastOcrMethod == "BitmapCompare" && comboBoxOcrMethod.Items.Count > 1)
                comboBoxOcrMethod.SelectedIndex = 1;
            else if (Configuration.Settings.VobSubOcr.LastOcrMethod == "MODI" && comboBoxOcrMethod.Items.Count > 2)
                comboBoxOcrMethod.SelectedIndex = 2;
            else
                comboBoxOcrMethod.SelectedIndex = 0;

            _bluRaySubtitlesOriginal = subtitles;

            groupBoxImagePalette.Visible = false;

            Text = Configuration.Settings.Language.VobSubOcr.TitleBluRay;
            checkBoxAutoTransparentBackground.Checked = false;
            checkBoxAutoTransparentBackground.Visible = false;
        }

        private void LoadImageCompareCharacterDatabaseList()
        {
            try
            {
                string characterDatabasePath = Configuration.VobSubCompareFolder.TrimEnd(Path.DirectorySeparatorChar);
                if (!Directory.Exists(characterDatabasePath))
                    Directory.CreateDirectory(characterDatabasePath);

                comboBoxCharacterDatabase.Items.Clear();

                foreach (string dir in Directory.GetDirectories(characterDatabasePath))
                    comboBoxCharacterDatabase.Items.Add(Path.GetFileName(dir));

                if (comboBoxCharacterDatabase.Items.Count == 0)
                {
                    Directory.CreateDirectory(characterDatabasePath + Path.DirectorySeparatorChar + _vobSubOcrSettings.LastImageCompareFolder);
                    comboBoxCharacterDatabase.Items.Add(_vobSubOcrSettings.LastImageCompareFolder);
                }

                for (int i = 0; i < comboBoxCharacterDatabase.Items.Count; i++)
                {
                    if (string.Compare(comboBoxCharacterDatabase.Items[i].ToString(), _vobSubOcrSettings.LastImageCompareFolder, true) == 0)
                        comboBoxCharacterDatabase.SelectedIndex = i;
                }
                if (comboBoxCharacterDatabase.SelectedIndex < 0)
                    comboBoxCharacterDatabase.SelectedIndex = 0;

            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Configuration.Settings.Language.VobSubOcr.UnableToCreateCharacterDatabaseFolder, ex.Message));
            }            
        }

        private void LoadImageCompareBitmaps()
        {
            _compareBitmaps = new List<CompareItem>();
            string path = Configuration.VobSubCompareFolder + comboBoxCharacterDatabase.SelectedItem + Path.DirectorySeparatorChar;
            if (!File.Exists(path + "CompareDescription.xml"))
                _compareDoc.LoadXml("<OcrBitmaps></OcrBitmaps>");
            else
                _compareDoc.Load(path + "CompareDescription.xml");

            foreach (string bmpFileName in Directory.GetFiles(path, "*.bmp"))
            {
                string name = Path.GetFileNameWithoutExtension(bmpFileName);

                XmlNode node = _compareDoc.DocumentElement.SelectSingleNode("FileName[.='" + name + "']");
                if (node != null)
                {                    
                    bool isItalic = node.Attributes["Italic"] != null;
                    int expandCount = 0;
                    if (node.Attributes["Expand"] != null)
                    {
                        if (!int.TryParse(node.Attributes["Expand"].InnerText, out expandCount))
                            expandCount = 0;
                    }

                    Bitmap bmp = null;
                    using (var ms = new MemoryStream(File.ReadAllBytes(bmpFileName))) // load bmp without file lock
                    {
                        bmp = (Bitmap)Bitmap.FromStream(ms);
                    }

                    _compareBitmaps.Add(new CompareItem(bmp, name, isItalic, expandCount));
                }
                else
                {
                    try
                    {
                        File.Delete(bmpFileName);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private bool InitializeSubIdx(string vobSubFileName)
        {
            VobSubParser vobSubParser = new VobSubParser(true);
            string idxFileName = Path.ChangeExtension(vobSubFileName, ".idx");
            vobSubParser.OpenSubIdx(vobSubFileName, idxFileName);
            _vobSubMergedPackist = vobSubParser.MergeVobSubPacks();
            _palette = vobSubParser.IdxPalette;

            List<int> languageStreamIds = new List<int>();
            foreach (var pack in _vobSubMergedPackist)
            {
                if (pack.SubPicture.Delay.TotalMilliseconds > 500 && !languageStreamIds.Contains(pack.StreamId))
                    languageStreamIds.Add(pack.StreamId);
            }
            if (languageStreamIds.Count > 1)
            {
                DvdSubRipChooseLanguage ChooseLanguage = new DvdSubRipChooseLanguage();
                ChooseLanguage.Initialize(_vobSubMergedPackist, _palette, vobSubParser.IdxLanguages, string.Empty);
                if (ChooseLanguage.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    _vobSubMergedPackist = ChooseLanguage.SelectedVobSubMergedPacks;

                    SetTesseractLanguageFromLanguageString(ChooseLanguage.SelectedLanguageString);

                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void SetTesseractLanguageFromLanguageString(string languageString)
        {
            // try to match language from vob to tesseract language
            if (comboBoxTesseractLanguages.SelectedIndex >= 0 && comboBoxTesseractLanguages.Items.Count > 1 && languageString != null)
            {
                languageString = languageString.ToLower();
                for (int i = 0; i < comboBoxTesseractLanguages.Items.Count; i++)
                {
                    TesseractLanguage tl = (comboBoxTesseractLanguages.Items[i] as TesseractLanguage);
                    if (tl.Text.StartsWith("Chinese") && (languageString.StartsWith("chinese") || languageString.StartsWith("中文")))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    if (tl.Text.StartsWith("Korean") && (languageString.StartsWith("korean") || languageString.StartsWith("한국어")))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Swedish") && languageString.StartsWith("svenska"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Norwegian") && languageString.StartsWith("norsk"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Dutch") && languageString.StartsWith("Nederlands"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Danish") && languageString.StartsWith("dansk"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("English") && languageString.StartsWith("english"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("French") && (languageString.StartsWith("french") || languageString.StartsWith("français")))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Spannish") && (languageString.StartsWith("spannish") || languageString.StartsWith("españo")))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Finnish") && languageString.StartsWith("suomi"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Italian") && languageString.StartsWith("itali"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("German") && languageString.StartsWith("deutsch"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                    else if (tl.Text.StartsWith("Portuguese") && languageString.StartsWith("português"))
                    {
                        comboBoxTesseractLanguages.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void LoadBdnXml()
        {
            _subtitle = new Subtitle();

            _bdnXmlSubtitle = new Subtitle();
            int max = _bdnXmlOriginal.Paragraphs.Count;
            for (int i = 0; i < max; i++)
            {
                var x = _bdnXmlOriginal.Paragraphs[i];
                if ((checkBoxShowOnlyForced.Checked && x.Forced) ||
                    checkBoxShowOnlyForced.Checked == false)
                {
                    _bdnXmlSubtitle.Paragraphs.Add(new Paragraph(x));
                    Paragraph p = new Paragraph(x);
                    p.Text = string.Empty;
                    _subtitle.Paragraphs.Add(p);
                }
            }
            _subtitle.Renumber(1);

            FixShortDisplayTimes(_subtitle);

            subtitleListView1.Fill(_subtitle);
            subtitleListView1.SelectIndexAndEnsureVisible(0);

            numericUpDownStartNumber.Maximum = max;
            if (numericUpDownStartNumber.Maximum > 0 && numericUpDownStartNumber.Minimum <= 1)
                numericUpDownStartNumber.Value = 1;

            buttonOK.Enabled = true;
            buttonCancel.Enabled = true;
            buttonStartOcr.Enabled = true;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = true;
            buttonEditCharacterDatabase.Enabled = true;
            buttonStartOcr.Focus();            
        }

        private void LoadBluRaySup()
        {
            _subtitle = new Subtitle();

            _bluRaySubtitles = new List<Logic.BluRaySup.BluRaySupPicture>();
            int max = _bluRaySubtitlesOriginal.Count;
            for (int i = 0; i < max; i++)
            {
                var x = _bluRaySubtitlesOriginal[i];
                if ((checkBoxShowOnlyForced.Checked && x.IsForced) ||
                    checkBoxShowOnlyForced.Checked == false)
                {
                    _bluRaySubtitles.Add(x);
                    Paragraph p = new Paragraph();
                    p.StartTime = new TimeCode(TimeSpan.FromMilliseconds((x.StartTime + 45) / 90.0));
                    p.EndTime = new TimeCode(TimeSpan.FromMilliseconds((x.EndTime + 45) / 90.0));
                    _subtitle.Paragraphs.Add(p);
                }
            }
            _subtitle.Renumber(1);

            FixShortDisplayTimes(_subtitle);

            subtitleListView1.Fill(_subtitle);
            subtitleListView1.SelectIndexAndEnsureVisible(0);

            numericUpDownStartNumber.Maximum = max;
            if (numericUpDownStartNumber.Maximum > 0 && numericUpDownStartNumber.Minimum <= 1)
                numericUpDownStartNumber.Value = 1;

            buttonOK.Enabled = true;
            buttonCancel.Enabled = true;
            buttonStartOcr.Enabled = true;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = true;
            buttonEditCharacterDatabase.Enabled = true;
            buttonStartOcr.Focus();
        }

        private void LoadVobRip()
        {
            _subtitle = new Subtitle();
            _vobSubMergedPackist = new List<VobSubMergedPack>();
            int max = _vobSubMergedPackistOriginal.Count;
            for (int i = 0; i < max; i++)
            {
                var x = _vobSubMergedPackistOriginal[i];
                if ((checkBoxShowOnlyForced.Checked && x.SubPicture.Forced) ||
                    checkBoxShowOnlyForced.Checked == false)
                {
                    _vobSubMergedPackist.Add(x);
                    Paragraph p = new Paragraph(string.Empty, x.StartTime.TotalMilliseconds, x.EndTime.TotalMilliseconds);
                    if (checkBoxUseTimeCodesFromIdx.Checked && x.IdxLine != null)
                    {
                        double durationMilliseconds = p.Duration.TotalMilliseconds;
                        p.StartTime = new TimeCode(TimeSpan.FromMilliseconds(x.IdxLine.StartTime.TotalMilliseconds));
                        p.EndTime = new TimeCode(TimeSpan.FromMilliseconds(x.IdxLine.StartTime.TotalMilliseconds + durationMilliseconds));
                    }
                    _subtitle.Paragraphs.Add(p);
                }
            }
            _subtitle.Renumber(1);

            FixShortDisplayTimes(_subtitle);

            subtitleListView1.Fill(_subtitle);
            subtitleListView1.SelectIndexAndEnsureVisible(0);

            numericUpDownStartNumber.Maximum = max;
            if (numericUpDownStartNumber.Maximum > 0 && numericUpDownStartNumber.Minimum <= 1)
                numericUpDownStartNumber.Value = 1;

            buttonOK.Enabled = true;
            buttonCancel.Enabled = true;
            buttonStartOcr.Enabled = true;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = true;
            buttonEditCharacterDatabase.Enabled = true;
            buttonStartOcr.Focus();
        }

        public void FixShortDisplayTimes(Subtitle subtitle)
        {
            for (int i = 0; i < subtitle.Paragraphs.Count; i++)
            {
                Paragraph p = _subtitle.Paragraphs[i];
                if (p.EndTime.TotalMilliseconds <= p.StartTime.TotalMilliseconds)
                {
                    Paragraph next = _subtitle.GetParagraphOrDefault(i + 1);
                    double newEndTime = p.StartTime.TotalMilliseconds + Configuration.Settings.VobSubOcr.DefaultMillisecondsForUnknownDurations;
                    if (next == null || (newEndTime < next.StartTime.TotalMilliseconds))
                        p.EndTime.TotalMilliseconds = newEndTime;
                    else if (next != null)
                        p.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds -1;
                }
            }
        }

        private Bitmap GetSubtitleBitmap(int index)
        {
            if (_bdnXmlSubtitle != null)
            {
                if (index >= 0 && index < _bdnXmlSubtitle.Paragraphs.Count)
                {
                    string fileName = Path.Combine(Path.GetDirectoryName(_bdnFileName), _bdnXmlSubtitle.Paragraphs[index].Text);
                    if (File.Exists(fileName))
                    {
                        Bitmap b = new Bitmap(fileName);
                        if (checkBoxAutoTransparentBackground.Checked)
                            b.MakeTransparent();
                        return b;
                    }
                }
                return null;
            }

            if (_bluRaySubtitlesOriginal != null)
            {
                if (_bluRaySubtitles[index].Palettes.Count == 0 && _defaultPaletteInfo == null)
                {
                    for (int i = 0; i < _bluRaySubtitlesOriginal.Count; i++)
                    {
                        if (_bluRaySubtitlesOriginal[i].Palettes.Count > 0)
                        {
                            _defaultPaletteInfo = _bluRaySubtitlesOriginal[i].DecodePalette(null);
                        }
                    }
                }
                return _bluRaySubtitles[index].DecodeImage(_defaultPaletteInfo);
            }
            else if (checkBoxCustomFourColors.Checked)
            {
                Color background = pictureBoxBackground.BackColor;
                Color pattern = pictureBoxPattern.BackColor;
                Color emphasis1 = pictureBoxEmphasis1.BackColor;
                Color emphasis2 = pictureBoxEmphasis2.BackColor;

                if (checkBoxBackgroundTransparent.Checked)
                    background = Color.Transparent;
                if (checkBoxPatternTransparent.Checked)
                    pattern = Color.Transparent;
                if (checkBoxEmphasis1Transparent.Checked)
                    emphasis1 = Color.Transparent;
                if (checkBoxEmphasis2Transparent.Checked)
                    emphasis2 = Color.Transparent;

                Bitmap bm = _vobSubMergedPackist[index].SubPicture.GetBitmap(null, background, pattern, emphasis1, emphasis2, true);
                if (checkBoxAutoTransparentBackground.Checked)
                    bm.MakeTransparent();
                return bm;
            }

            Bitmap bmp = _vobSubMergedPackist[index].SubPicture.GetBitmap(_palette, Color.Transparent, Color.Black, Color.White, Color.Black, false);
            if (checkBoxAutoTransparentBackground.Checked)
                bmp.MakeTransparent();
            return bmp;
        }

        private long GetSubtitleStartTimeMilliseconds(int index)
        {
            if (_bdnXmlSubtitle != null)
                return (long)_bdnXmlSubtitle.Paragraphs[index].StartTime.TotalMilliseconds;
            else if (_bluRaySubtitlesOriginal != null)
                return (_bluRaySubtitles[index].StartTime + 45) / 90;
            else 
                return (long)_vobSubMergedPackist[index].StartTime.TotalMilliseconds;
        }      
      
        private long GetSubtitleEndTimeMilliseconds(int index)
        {
            if (_bdnXmlSubtitle != null)
                return (long)_bdnXmlSubtitle.Paragraphs[index].EndTime.TotalMilliseconds;
            else if (_bluRaySubtitlesOriginal != null)
                return (_bluRaySubtitles[index].EndTime + 45) / 90;
            else
                return (long)_vobSubMergedPackist[index].EndTime.TotalMilliseconds;
        }

        private int GetSubtitleCount()
        {
            if (_bdnXmlSubtitle != null)
                return _bdnXmlSubtitle.Paragraphs.Count;
            else if (_bluRaySubtitlesOriginal != null)
                return _bluRaySubtitles.Count;
            else
                return _vobSubMergedPackist.Count;
        }

        private void ShowSubtitleImage(int index)
        {
            int numberOfImages = GetSubtitleCount();

            if (index < numberOfImages)
            {
                groupBoxSubtitleImage.Text = string.Format(Configuration.Settings.Language.VobSubOcr.SubtitleImageXofY, index + 1, numberOfImages);
                pictureBoxSubtitleImage.Image = GetSubtitleBitmap(index); 
                pictureBoxSubtitleImage.Refresh();
            }
            else
            {
                groupBoxSubtitleImage.Text = Configuration.Settings.Language.VobSubOcr.SubtitleImage;
                pictureBoxSubtitleImage.Image = new Bitmap(1, 1);
            }
        }

        private CompareMatch GetCompareMatch(ImageSplitterItem targetItem, Bitmap parentBitmap, out CompareMatch secondBestGuess)
        {
            secondBestGuess = null;
            int index = 0;
            int smallestDifference = 10000;
            int smallestIndex = -1;
            Bitmap target = targetItem.Bitmap;
            
            foreach (CompareItem compareItem in _compareBitmaps)
            {
                // check for expand match!
                if (compareItem.ExpandCount > 0 && compareItem.Bitmap.Width > targetItem.Bitmap.Width)
                {
                    int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, ImageSplitter.Copy(parentBitmap, new Rectangle(targetItem.X, targetItem.Y, compareItem.Bitmap.Width, compareItem.Bitmap.Height)));
                    if (dif < smallestDifference)
                    {
                        smallestDifference = dif;
                        smallestIndex = index;
                        if (dif == 0)
                            break; // foreach ending
                    }
                }
                index++;
            }

            // Search images with minor location changes
            FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, target);

            if (smallestDifference > 0 && target.Width > 12)
            {
                Bitmap cutBitmap = CopyBitmapSection(target, new Rectangle(1, 0, target.Width - 2, target.Height));
                FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, cutBitmap);
                cutBitmap.Dispose();
            }

            if (smallestDifference > 0 && target.Width > 12)
            {
                Bitmap cutBitmap = CopyBitmapSection(target, new Rectangle(0, 0, target.Width - 2, target.Height));
                FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, cutBitmap);
                cutBitmap.Dispose();
            }

            if (smallestDifference > 0 && target.Width > 12)
            {
                Bitmap cutBitmap = CopyBitmapSection(target, new Rectangle(1, 0, target.Width - 2, target.Height));
                int topCrop = 0;
                cutBitmap = ImageSplitter.CropTopAndBottom(cutBitmap, out topCrop, 2);
                if (cutBitmap.Height != target.Height)
                    FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, cutBitmap);
                cutBitmap.Dispose();
            }

            if (smallestDifference > 0 && target.Width > 15)
            {
                Bitmap cutBitmap = CopyBitmapSection(target, new Rectangle(1, 0, target.Width - 2, target.Height));
                int topCrop = 0;
                cutBitmap = ImageSplitter.CropTopAndBottom(cutBitmap, out topCrop);
                if (cutBitmap.Height != target.Height)
                    FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, cutBitmap);
                cutBitmap.Dispose();
            }                      


            if (smallestDifference > 0 && target.Width > 15)
            {
                Bitmap cutBitmap = CopyBitmapSection(target, new Rectangle(1, 0, target.Width - 2, target.Height));
                int topCrop = 0;
                cutBitmap = ImageSplitter.CropTopAndBottom(cutBitmap, out topCrop);
                if (cutBitmap.Height != target.Height)
                    FindBestMatch(ref index, ref smallestDifference, ref smallestIndex, cutBitmap);
                cutBitmap.Dispose();
            }                      

            if (smallestIndex >= 0)
            {
                double differencePercentage = smallestDifference * 100.0 / (target.Width * target.Height);
                double maxDiff= _vobSubOcrSettings.AllowDifferenceInPercent; // should be around 1.0 for vob/sub...
                if (_bluRaySubtitlesOriginal != null)
                    maxDiff = 12.9; // let bluray sup have a 12.9% diff
                if (differencePercentage < maxDiff) //_vobSubOcrSettings.AllowDifferenceInPercent) // should be around 1.0...
                {
                    XmlNode node = _compareDoc.DocumentElement.SelectSingleNode("FileName[.='" + _compareBitmaps[smallestIndex].Name + "']");
                    if (node != null)
                    {
                        bool isItalic = node.Attributes["Italic"] != null;

                        int expandCount = 0;
                        if (node.Attributes["Expand"] != null)
                        {
                            if (!int.TryParse(node.Attributes["Expand"].InnerText, out expandCount))
                                expandCount = 0;
                        }
                        return new CompareMatch(node.Attributes["Text"].InnerText, isItalic, expandCount, _compareBitmaps[smallestIndex].Name);
                    }
                }

                XmlNode nodeGuess = _compareDoc.DocumentElement.SelectSingleNode("FileName[.='" + _compareBitmaps[smallestIndex].Name + "']");
                bool isItalicGuess = nodeGuess.Attributes["Italic"] != null;
                int expandCountGuess = 0;
                if (nodeGuess.Attributes["Expand"] != null)
                {
                    if (!int.TryParse(nodeGuess.Attributes["Expand"].InnerText, out expandCountGuess))
                        expandCountGuess = 0;
                }
                secondBestGuess = new CompareMatch(nodeGuess.Attributes["Text"].InnerText, isItalicGuess, expandCountGuess, _compareBitmaps[smallestIndex].Name);
            }

            return null;
        }

        static public Bitmap CopyBitmapSection(Bitmap srcBitmap, Rectangle section)
        {
            Bitmap bmp = new Bitmap(section.Width, section.Height);
            Graphics g = Graphics.FromImage(bmp);
            g.DrawImage(srcBitmap, 0, 0, section, GraphicsUnit.Pixel);
            g.Dispose();
            return bmp;
        }

        private void FindBestMatch(ref int index, ref int smallestDifference, ref int smallestIndex, Bitmap target)
        {
            if (smallestDifference > 0)
            {
                index = 0;
                foreach (CompareItem compareItem in _compareBitmaps)
                {
                    if (compareItem.Bitmap.Width == target.Width && compareItem.Bitmap.Height == target.Height) // precise math in size
                    {
                        int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                        if (dif < smallestDifference)
                        {
                            smallestDifference = dif;
                            smallestIndex = index;
                            if (dif == 0)
                                break; // foreach ending
                        }
                    }
                    index++;
                }
            }

            if (target.Width > 5) // for other than very narrow letter (like 'i' and 'l' and 'I'), try more sizes
            {
                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width && compareItem.Bitmap.Height == target.Height - 1)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width && compareItem.Bitmap.Height == target.Height + 1)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(target, compareItem.Bitmap);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width + 1 && compareItem.Bitmap.Height == target.Height + 1)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(target, compareItem.Bitmap);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width - 1 && compareItem.Bitmap.Height == target.Height)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width - 1 && compareItem.Bitmap.Height == target.Height - 1)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width - 1 == target.Width && compareItem.Bitmap.Height == target.Height)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(target, compareItem.Bitmap);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0 && target.Width > 10)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width - 2 && compareItem.Bitmap.Height == target.Height)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference >0 && target.Width > 12)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width - 3 && compareItem.Bitmap.Height == target.Height)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0 && target.Width > 12)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width == target.Width && compareItem.Bitmap.Height == target.Height - 3)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(compareItem.Bitmap, target);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }

                if (smallestDifference > 0)
                {
                    index = 0;
                    foreach (CompareItem compareItem in _compareBitmaps)
                    {
                        if (compareItem.Bitmap.Width - 2 == target.Width && compareItem.Bitmap.Height == target.Height)
                        {
                            int dif = ImageSplitter.IsBitmapsAlike(target, compareItem.Bitmap);
                            if (dif < smallestDifference)
                            {
                                smallestDifference = dif;
                                smallestIndex = index;
                                if (dif == 0)
                                    break; // foreach ending
                            }
                        }
                        index++;
                    }
                }
            }
        }

        private string SaveCompareItem(Bitmap newTarget, string text, bool isItalic, int expandCount)
        {
            string path = Configuration.VobSubCompareFolder + comboBoxCharacterDatabase.SelectedItem + Path.DirectorySeparatorChar;
            string name = Guid.NewGuid().ToString();
            string fileName = path + name + ".bmp";
            newTarget.Save(fileName);

            _compareBitmaps.Add(new CompareItem(newTarget, name, isItalic, expandCount));

            XmlElement element = _compareDoc.CreateElement("FileName");
            XmlAttribute attribute = _compareDoc.CreateAttribute("Text");
            attribute.InnerText = text;
            element.Attributes.Append(attribute);
            if (expandCount > 0)
            {
                XmlAttribute expandSelection = _compareDoc.CreateAttribute("Expand");
                expandSelection.InnerText = expandCount.ToString();
                element.Attributes.Append(expandSelection);
            }
            if (isItalic)
            {
                XmlAttribute italic = _compareDoc.CreateAttribute("Italic");
                italic.InnerText = "true";
                element.Attributes.Append(italic);
            }
            element.InnerText = name;
            _compareDoc.DocumentElement.AppendChild(element);
            _compareDoc.Save(path + "CompareDescription.xml");
            return name;
        }

        /// <summary>
        /// Ocr via image compare
        /// </summary>
        private string SplitAndOcrBitmapNormal(Bitmap bitmap, int listViewIndex)
        {
            if (_ocrFixEngine == null)
                LoadOcrFixEngine();

            var matches = new List<CompareMatch>();
            List<ImageSplitterItem> list = ImageSplitter.SplitBitmapToLetters(bitmap, (int)numericUpDownPixelsIsSpace.Value, checkBoxRightToLeft.Checked, Configuration.Settings.VobSubOcr.TopToBottom);
            int index = 0;
            bool expandSelection = false;
            bool shrinkSelection = false;
            var expandSelectionList = new List<ImageSplitterItem>();
            while (index < list.Count)
            {                
                ImageSplitterItem item = list[index];
                if (expandSelection || shrinkSelection)
                {
                    expandSelection = false;
                    if (shrinkSelection && index > 0)
                    {
                        shrinkSelection = false;
                    }
                    else if (index+1 < list.Count && list[index+1].Bitmap != null) // only allow expand to EndOfLine or space
                    {
                        
                        index++; 
                        expandSelectionList.Add(list[index]);
                    }
                    item = GetExpandedSelection(bitmap, expandSelectionList);

                    _vobSubOcrCharacter.Initialize(bitmap, item, _manualOcrDialogPosition, _italicCheckedLast, expandSelectionList.Count > 1, null, _lastAdditions, this);
                    DialogResult result = _vobSubOcrCharacter.ShowDialog(this);
                    _manualOcrDialogPosition = _vobSubOcrCharacter.FormPosition;
                    if (result == DialogResult.OK && _vobSubOcrCharacter.ShrinkSelection)
                    {
                        shrinkSelection = true;
                        index--;
                        if (expandSelectionList.Count > 0)
                            expandSelectionList.RemoveAt(expandSelectionList.Count - 1);
                    }
                    else if (result == DialogResult.OK && _vobSubOcrCharacter.ExpandSelection)
                    {
                        expandSelection = true;
                    }
                    else if (result == DialogResult.OK)
                    {
                        string text = _vobSubOcrCharacter.ManualRecognizedCharacters;
                        string name = SaveCompareItem(item.Bitmap, text, _vobSubOcrCharacter.IsItalic, expandSelectionList.Count);
                        var addition = new ImageCompareAddition(name, text, item.Bitmap, _vobSubOcrCharacter.IsItalic, listViewIndex);
                        _lastAdditions.Add(addition);
                        matches.Add(new CompareMatch(text, _vobSubOcrCharacter.IsItalic, expandSelectionList.Count, null));
                        expandSelectionList = new List<ImageSplitterItem>();
                    }
                    else if (result == DialogResult.Abort)
                    {
                        _abort = true;
                    }
                    else
                    {
                        matches.Add(new CompareMatch("*", false, 0, null));
                    }
                    _italicCheckedLast = _vobSubOcrCharacter.IsItalic;

                }
                else if (item.Bitmap == null)
                {
                    matches.Add(new CompareMatch(item.SpecialCharacter, false, 0, null));
                }
                else
                {
                    CompareMatch bestGuess;
                    CompareMatch match = GetCompareMatch(item, bitmap, out bestGuess);
                    if (match == null)
                    {
                        _vobSubOcrCharacter.Initialize(bitmap, item, _manualOcrDialogPosition, _italicCheckedLast, false, bestGuess, _lastAdditions, this);
                        DialogResult result = _vobSubOcrCharacter.ShowDialog(this);
                        _manualOcrDialogPosition = _vobSubOcrCharacter.FormPosition;
                        if (result == DialogResult.OK && _vobSubOcrCharacter.ExpandSelection)
                        {
                            expandSelectionList.Add(item);
                            expandSelection = true;
                        }
                        else if (result == DialogResult.OK)
                        {
                            string text = _vobSubOcrCharacter.ManualRecognizedCharacters;
                            string name = SaveCompareItem(item.Bitmap, text, _vobSubOcrCharacter.IsItalic, 0);
                            var addition = new ImageCompareAddition(name, text, item.Bitmap, _vobSubOcrCharacter.IsItalic, listViewIndex);
                            _lastAdditions.Add(addition);
                            matches.Add(new CompareMatch(text, _vobSubOcrCharacter.IsItalic, 0, null));
                        }
                        else if (result == DialogResult.Abort)
                        {
                            _abort = true;
                        }
                        else
                        {
                            matches.Add(new CompareMatch("*", false, 0, null));
                        }
                        _italicCheckedLast = _vobSubOcrCharacter.IsItalic;
                    }
                    else // found image match
                    {
                        matches.Add(new CompareMatch(match.Text, match.Italic, 0, null));
                        if (match.ExpandCount > 0)
                            index += match.ExpandCount - 1;
                    }
                }
                if (_abort)
                    return string.Empty;
                if (!expandSelection && ! shrinkSelection)
                    index++;
                if (shrinkSelection && expandSelectionList.Count < 2)
                {
                    //index--;
                    shrinkSelection = false;
                    expandSelectionList = new List<ImageSplitterItem>();
                }
            }
            string line = GetStringWithItalicTags(matches);
            if (checkBoxAutoFixCommonErrors.Checked)
                line = OcrFixEngine.FixOcrErrorsViaHardcodedRules(line, _lastLine, null); // TODO: add abbreviations list

            if (checkBoxRightToLeft.Checked)
                line = ReverseNumberStrings(line);


            //ocr fix engine
            string textWithOutFixes = line;
            if (_ocrFixEngine.IsDictionaryLoaded)
            {
                if (checkBoxAutoFixCommonErrors.Checked)
                    line = _ocrFixEngine.FixOcrErrors(line, index, _lastLine, true, checkBoxGuessUnknownWords.Checked);
                int correctWords;
                int wordsNotFound = _ocrFixEngine.CountUnknownWordsViaDictionary(line, out correctWords);                

                if (wordsNotFound > 0 || correctWords == 0 || textWithOutFixes != null && textWithOutFixes.ToString().Replace("~", string.Empty).Trim().Length == 0)
                {
                    _ocrFixEngine.AutoGuessesUsed.Clear();
                    _ocrFixEngine.UnknownWordsFound.Clear();
                    line = _ocrFixEngine.FixUnknownWordsViaGuessOrPrompt(out wordsNotFound, line, listViewIndex, bitmap, checkBoxAutoFixCommonErrors.Checked, checkBoxPromptForUnknownWords.Checked, true, checkBoxGuessUnknownWords.Checked);
                }

                if (_ocrFixEngine.Abort)
                {
                    ButtonStopClick(null, null);
                    _ocrFixEngine.Abort = false;
                    return string.Empty;
                }

                // Log used word guesses (via word replace list)
                foreach (string guess in _ocrFixEngine.AutoGuessesUsed)
                    listBoxLogSuggestions.Items.Add(guess);
                _ocrFixEngine.AutoGuessesUsed.Clear();

                // Log unkown words guess (found via spelling dictionaries)
                foreach (string unknownWord in _ocrFixEngine.UnknownWordsFound)
                    listBoxUnknownWords.Items.Add(unknownWord);
                _ocrFixEngine.UnknownWordsFound.Clear();

                if (wordsNotFound >= 3)
                    subtitleListView1.SetBackgroundColor(listViewIndex, Color.Red);
                if (wordsNotFound == 2)
                    subtitleListView1.SetBackgroundColor(listViewIndex, Color.Orange);
                else if (wordsNotFound == 1)
                    subtitleListView1.SetBackgroundColor(listViewIndex, Color.Yellow);
                else if (line.Trim().Length == 0)
                    subtitleListView1.SetBackgroundColor(listViewIndex, Color.Orange);
                else
                    subtitleListView1.SetBackgroundColor(listViewIndex, Color.Green);
            }

            if (textWithOutFixes.Trim() != line.Trim())
            {
                _tessnetOcrAutoFixes++;
                labelFixesMade.Text = string.Format(" - {0}", _tessnetOcrAutoFixes);
                LogOcrFix(listViewIndex, textWithOutFixes.ToString(), line);
            }

            return line;
        }

        private string ReverseNumberStrings(string line)
        {
            Regex regex = new Regex(@"\b\d+\b");
            var matches = regex.Matches(line);
            foreach (Match match in matches)
            {
                if (match.Length > 1)
                {
                    string number = string.Empty;
                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        number = line[i] + number;
                    }
                    line = line.Remove(match.Index, match.Length).Insert(match.Index, number);
                }
            }
            return line;
        }

        private ImageSplitterItem GetExpandedSelection(Bitmap bitmap, List<ImageSplitterItem> expandSelectionList)
        {
            if (checkBoxRightToLeft.Checked)
            {
                int minimumX = expandSelectionList[expandSelectionList.Count - 1].X - expandSelectionList[expandSelectionList.Count - 1].Bitmap.Width;
                int maximumX = expandSelectionList[0].X;
                int minimumY = expandSelectionList[0].Y;
                int maximumY = expandSelectionList[0].Y + expandSelectionList[0].Bitmap.Height;
                foreach (ImageSplitterItem item in expandSelectionList)
                {
                    if (item.Y < minimumY)
                        minimumY = item.Y;
                    if (item.Y + item.Bitmap.Height > maximumY)
                        maximumY = item.Y + item.Bitmap.Height;
                }
                Bitmap part = ImageSplitter.Copy(bitmap, new Rectangle(minimumX, minimumY, maximumX - minimumX, maximumY - minimumY));
                return new ImageSplitterItem(minimumX, minimumY, part);
            }
            else
            {
                int minimumX = expandSelectionList[0].X;
                int maximumX = expandSelectionList[expandSelectionList.Count - 1].X + expandSelectionList[expandSelectionList.Count - 1].Bitmap.Width;
                int minimumY = expandSelectionList[0].Y;
                int maximumY = expandSelectionList[0].Y + expandSelectionList[0].Bitmap.Height;
                foreach (ImageSplitterItem item in expandSelectionList)
                {
                    if (item.Y < minimumY)
                        minimumY = item.Y;
                    if (item.Y + item.Bitmap.Height > maximumY)
                        maximumY = item.Y + item.Bitmap.Height;
                }
                Bitmap part = ImageSplitter.Copy(bitmap, new Rectangle(minimumX, minimumY, maximumX - minimumX, maximumY - minimumY));
                return new ImageSplitterItem(minimumX, minimumY, part);
            }
        }

        private static string GetStringWithItalicTags(List<CompareMatch> matches)
        {
            StringBuilder paragraph = new StringBuilder();
            StringBuilder line = new StringBuilder();
            StringBuilder word = new StringBuilder();
            int lettersItalics = 0;
            int lettersNonItalics = 0;
            int lineLettersNonItalics = 0;
            int wordItalics = 0;
            int wordNonItalics = 0;
            bool isItalic = false;
            bool allItalic = true;
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].Text == " ")
                {
                    ItalicsWord(line, ref word, ref lettersItalics, ref lettersNonItalics, ref wordItalics, ref wordNonItalics, ref isItalic, " ");
                }
                else if (matches[i].Text == Environment.NewLine)
                {
                    ItalicsWord(line, ref word, ref lettersItalics, ref lettersNonItalics, ref wordItalics, ref wordNonItalics, ref isItalic, "");
                    ItalianLine(paragraph, ref line, ref allItalic, ref wordItalics, ref wordNonItalics, ref isItalic, Environment.NewLine, lineLettersNonItalics);
                    lineLettersNonItalics = 0;
                }
                else if (matches[i].Italic)
                {
                    word.Append(matches[i].Text);
                    lettersItalics += matches[i].Text.Length;
                    lineLettersNonItalics += matches[i].Text.Length;
                }
                else
                {
                    word.Append(matches[i].Text);
                    lettersNonItalics += matches[i].Text.Length;
                }
            }

            if (word.Length > 0)
                ItalicsWord(line, ref word, ref lettersItalics, ref lettersNonItalics, ref wordItalics, ref wordNonItalics, ref isItalic, "");
            if (line.Length > 0)
                ItalianLine(paragraph, ref line, ref allItalic, ref wordItalics, ref wordNonItalics, ref isItalic, "", lineLettersNonItalics);

            if (allItalic && matches.Count > 0)
            {
                string temp = paragraph.ToString().Replace("<i>", "").Replace("</i>", "");
                paragraph = new StringBuilder();
                paragraph.Append("<i>" + temp + "</i>");

            }

            return paragraph.ToString();
        }

        private static void ItalianLine(StringBuilder paragraph, ref StringBuilder line, ref bool allItalic, ref int wordItalics, ref int wordNonItalics, ref bool isItalic, string appendString, int lineLettersNonItalics)
        {
            if (isItalic)
            {
                line.Append("</i>");
                isItalic = false;
            }

            if (wordItalics > 0 && wordNonItalics == 0)
            {
                string temp = line.ToString().Replace("<i>", "").Replace("</i>", "");
                paragraph.Append("<i>" + temp + "</i>");
                paragraph.Append(appendString);
            }
            else if (wordItalics > 0 && wordNonItalics < 2 && lineLettersNonItalics < 3 && line.ToString().Trim().StartsWith("-"))
            {
                string temp = line.ToString().Replace("<i>", "").Replace("</i>", "");
                paragraph.Append("<i>" + temp + "</i>");
                paragraph.Append(appendString);
            }
            else
            {
                allItalic = false;

                if (wordItalics > 0)
                { 
                    string temp = line.ToString().Replace(" </i>", "</i> ");
                    line = new StringBuilder();
                    line.Append(temp);
                }

                paragraph.Append(line.ToString());
                paragraph.Append(appendString);
            }
            line = new StringBuilder();
            wordItalics = 0;
            wordNonItalics = 0;
        }

        private static void ItalicsWord(StringBuilder line, ref StringBuilder word, ref int lettersItalics, ref int lettersNonItalics, ref int wordItalics, ref int wordNonItalics, ref bool isItalic, string appendString)
        {
            if (lettersItalics >= lettersNonItalics && lettersItalics > 0)
            {
                if (!isItalic)
                    line.Append("<i>");
                line.Append(word + appendString);
                wordItalics++;
                isItalic = true;
            }
            else
            {
                if (isItalic)
                {
                    line.Append("</i>");
                    isItalic = false;
                }
                line.Append(word.ToString());
                line.Append(appendString);
                wordNonItalics++;
            }
            word = new StringBuilder();
            lettersItalics = 0;
            lettersNonItalics = 0;
        }

        public Subtitle SubtitleFromOcr
        {
            get
            {
                return _subtitle;
            }
        }

        private void FormVobSubOcr_Shown(object sender, EventArgs e)
        {
            if (_bdnXmlOriginal != null)
            {
                LoadBdnXml();
                bool hasForcedSubtitles = false;
                foreach (var x in _bdnXmlOriginal.Paragraphs)
                {
                    if (x.Forced)
                    {
                        hasForcedSubtitles = true;
                        break;
                    }
                }
                checkBoxShowOnlyForced.Enabled = hasForcedSubtitles;
                checkBoxUseTimeCodesFromIdx.Visible = false;
            }
            else if (_bluRaySubtitlesOriginal != null)
            {
                LoadBluRaySup();
                bool hasForcedSubtitles = false;
                foreach (var x in _bluRaySubtitlesOriginal)
                {
                    if (x.IsForced)
                    {
                        hasForcedSubtitles = true;
                        break;
                    }
                }
                checkBoxShowOnlyForced.Enabled = hasForcedSubtitles;
                checkBoxUseTimeCodesFromIdx.Visible = false;
            }
            else
            {
                _vobSubMergedPackistOriginal = new List<VobSubMergedPack>();
                bool hasIdxTimeCodes = false;
                bool hasForcedSubtitles = false;
                foreach (var x in _vobSubMergedPackist)
                {
                    _vobSubMergedPackistOriginal.Add(x);
                    if (x.IdxLine != null)
                        hasIdxTimeCodes = true;
                    if (x.SubPicture.Forced)
                        hasForcedSubtitles = true;
                }
                checkBoxUseTimeCodesFromIdx.CheckedChanged -= checkBoxUseTimeCodesFromIdx_CheckedChanged;
                checkBoxUseTimeCodesFromIdx.Visible = hasIdxTimeCodes;
                checkBoxUseTimeCodesFromIdx.Checked = hasIdxTimeCodes;
                checkBoxUseTimeCodesFromIdx.CheckedChanged += checkBoxUseTimeCodesFromIdx_CheckedChanged;
                checkBoxShowOnlyForced.Enabled = hasForcedSubtitles;
                LoadVobRip();
            }
            VobSubOcr_Resize(null, null);
        }

        private void ButtonOkClick(object sender, EventArgs e)
        {
            if (Configuration.Settings.VobSubOcr.XOrMorePixelsMakesSpace != (int)numericUpDownPixelsIsSpace.Value && _bluRaySubtitlesOriginal == null)
            {
                Configuration.Settings.VobSubOcr.XOrMorePixelsMakesSpace = (int)numericUpDownPixelsIsSpace.Value;    
                Configuration.Settings.Save();
            }
            DialogResult = DialogResult.OK;
        }

        private void SetButtonsEnabledAfterOcrDone()
        {
            buttonOK.Enabled = true;
            buttonCancel.Enabled = true;
            buttonStartOcr.Enabled = true;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = true;
            buttonEditCharacterDatabase.Enabled = true;

            labelStatus.Text = string.Empty;
            progressBar1.Visible = false;
        }

        private void ButtonStartOcrClick(object sender, EventArgs e)
        {
            Configuration.Settings.VobSubOcr.RightToLeft = checkBoxRightToLeft.Checked;
            _lastLine = null;
            buttonOK.Enabled = false;
            buttonCancel.Enabled = false;
            buttonStartOcr.Enabled = false;
            buttonStop.Enabled = true;
            buttonNewCharacterDatabase.Enabled = false;
            buttonEditCharacterDatabase.Enabled = false;

            _abort = false;

            int max = GetSubtitleCount();

            progressBar1.Maximum = max;
            progressBar1.Value = 0;
            progressBar1.Visible = true;
            for (int i = (int)numericUpDownStartNumber.Value-1; i < max; i++)
            {
                ShowSubtitleImage(i);

                var startTime = new TimeCode(TimeSpan.FromMilliseconds(GetSubtitleStartTimeMilliseconds(i)));
                var endTime = new TimeCode(TimeSpan.FromMilliseconds(GetSubtitleEndTimeMilliseconds(i)));
                labelStatus.Text = string.Format("{0} / {1}: {2} - {3}", i + 1, max, startTime, endTime);
                progressBar1.Value = i + 1;
                labelStatus.Refresh();
                progressBar1.Refresh();
                Application.DoEvents();
                if (_abort)
                {
                    SetButtonsEnabledAfterOcrDone();
                    return;
                }

                subtitleListView1.SelectIndexAndEnsureVisible(i);
                string text;
                if (comboBoxOcrMethod.SelectedIndex == 0)
                    text = OcrViaTessnet(GetSubtitleBitmap(i), i);
                else if (comboBoxOcrMethod.SelectedIndex == 1)
                    text = SplitAndOcrBitmapNormal(GetSubtitleBitmap(i), i);
                else 
                    text = CallModi(i);

                _lastLine = text;

                // max allow 2 lines
                if (checkBoxAutoBreakLines.Checked && text.Replace(Environment.NewLine, "*").Length + 2 <= text.Length)
                    text = Utilities.AutoBreakLine(text);               

                Application.DoEvents();
                if (_abort)
                {
                    textBoxCurrentText.Text = text;
                    SetButtonsEnabledAfterOcrDone();
                    return;
                }

                text = text.Trim();
                text = text.Replace("  ", " ");
                text = text.Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine);
                text = text.Replace("  ", " ");
                text = text.Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine);

                Paragraph p = _subtitle.GetParagraphOrDefault(i);
                if (p != null)
                    p.Text = text;
                textBoxCurrentText.Text = text;
            }            
            SetButtonsEnabledAfterOcrDone();            
        }

        private Bitmap ResizeBitmap(Bitmap b, int width, int height)
        {
            var result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
                g.DrawImage(b, 0, 0, width, height);
            return result;
        }

        //public void UnItalic(Bitmap bmp)
        //{
        //    double xOffset = 0;
        //    for (int y = 0; y < bmp.Height; y++)
        //    {
        //        int offset = (int)xOffset;
        //        for (int x = bmp.Width - 1; x >= 0; x--)
        //        {
        //            if (x - offset >= 0)
        //                bmp.SetPixel(x, y, bmp.GetPixel(x - offset, y));
        //            else
        //                bmp.SetPixel(x, y, Color.Transparent);
        //        }
        //        //                xOffset += 0.3;
        //        xOffset += 0.05;
        //    }
        //}

        //public void UnItalic(Bitmap bmp, double factor)
        //{
        //    int left = (int)(bmp.Height*factor);
        //    Bitmap unItaliced = new Bitmap(bmp.Width + left, bmp.Height);
        //    double xOffset = 0;
        //    for (int y = 0; y < bmp.Height; y++)
        //    {
        //        int offset = (int)xOffset;
        //        for (int x = bmp.Width - 1; x >= 0; x--)
        //        {
        //            if (x - offset >= 0)
        //                unItaliced.SetPixel(x, y, bmp.GetPixel(x - offset, y));
        //            else
        //                unItaliced.SetPixel(x, y, Color.Transparent);
        //        }
        //        //                xOffset += 0.3;
        //        xOffset += 0.05;
        //    }
        //}

        private string Tesseract3DoOcrViaExe(Bitmap bmp, string language)
        {
            string tempTiffFileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".png";
            bmp.Save(tempTiffFileName, System.Drawing.Imaging.ImageFormat.Png);
            string tempTextFileName = Path.GetTempPath() + Guid.NewGuid().ToString();

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(Configuration.TesseractFolder + "tesseract.exe");
			process.StartInfo.UseShellExecute = true;
            process.StartInfo.Arguments = "\"" + tempTiffFileName + "\" \"" + tempTextFileName + "\" -l " + language;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            if (Utilities.IsRunningOnLinux() || Utilities.IsRunningOnMac())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.FileName = "tesseract";
            }
            else
            {
                process.StartInfo.WorkingDirectory = (Configuration.TesseractFolder);
            }

            process.Start();
            process.WaitForExit(5000);

            string outputFileName = tempTextFileName + ".txt";
            string result = string.Empty;
            try
            {
                if (File.Exists(outputFileName))
                {
                    result = File.ReadAllText(outputFileName);
                    File.Delete(tempTextFileName + ".txt");
                }
                File.Delete(tempTiffFileName);
            }
            catch
            {
            }
            return result;
        }

        private string OcrViaTessnet(Bitmap bitmap, int index)
        {
            if (_ocrFixEngine == null)
                LoadOcrFixEngine();

            int badWords = 0;
            string textWithOutFixes = Tesseract3DoOcrViaExe(bitmap, _languageId);

            if (textWithOutFixes.ToString().Trim().Length == 0)
                textWithOutFixes = TesseractResizeAndRetry(bitmap);     

            int numberOfWords = textWithOutFixes.ToString().Split((" " + Environment.NewLine).ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Length;

            string line = textWithOutFixes.ToString().Trim();
            if (_ocrFixEngine.IsDictionaryLoaded)
            {
                if (checkBoxAutoFixCommonErrors.Checked)
                    line = _ocrFixEngine.FixOcrErrors(line, index, _lastLine, true, checkBoxGuessUnknownWords.Checked);
                int correctWords;
                int wordsNotFound = _ocrFixEngine.CountUnknownWordsViaDictionary(line, out correctWords);

                if (wordsNotFound > 0 || correctWords == 0)
                { 
                    string newUnfixedText = TesseractResizeAndRetry(bitmap);
                    string newText = _ocrFixEngine.FixOcrErrors(newUnfixedText, index, _lastLine, true, checkBoxGuessUnknownWords.Checked);
                    int newWordsNotFound = _ocrFixEngine.CountUnknownWordsViaDictionary(newText, out correctWords);
                    if (newWordsNotFound < wordsNotFound)
                    {
                        wordsNotFound = newWordsNotFound;
                        textWithOutFixes = newUnfixedText;
                        line = newText;
                    }
                }

                if (wordsNotFound > 0 || correctWords == 0 || textWithOutFixes != null && textWithOutFixes.ToString().Replace("~", string.Empty).Trim().Length == 0)
                {
                    _ocrFixEngine.AutoGuessesUsed.Clear();
                    _ocrFixEngine.UnknownWordsFound.Clear();

                    if (_modiEnabled && checkBoxUseModiInTesseractForUnknownWords.Checked)
                    {
                        // which is best - modi or tesseract - we find out here
                        string modiText = CallModi(index);

                        if (modiText.Length == 0)
                            modiText = CallModi(index); // retry... strange MODI
                        if (modiText.Length == 0)
                            modiText = CallModi(index); // retry... strange MODI                        

                        if (modiText.Length > 1)
                        {
                            int modiWordsNotFound = _ocrFixEngine.CountUnknownWordsViaDictionary(modiText, out correctWords);
                            if (modiWordsNotFound > 0)
                            {
                                string modiTextOcrFixed = modiText;
                                if (checkBoxAutoFixCommonErrors.Checked)
                                    modiTextOcrFixed = _ocrFixEngine.FixOcrErrors(modiText, index, _lastLine, false, checkBoxGuessUnknownWords.Checked);
                                int modiOcrCorrectedWordsNotFound = _ocrFixEngine.CountUnknownWordsViaDictionary(modiTextOcrFixed, out correctWords);
                                if (modiOcrCorrectedWordsNotFound < modiWordsNotFound)
                                    modiText = modiTextOcrFixed;
                            }

                            if (modiWordsNotFound < wordsNotFound)
                                line = modiText; // use the modi ocr'ed text
                        }

                        // take the best option - before ocr fixing, which we do again to save suggestions and prompt for user input
                        line = _ocrFixEngine.FixUnknownWordsViaGuessOrPrompt(out wordsNotFound, line, index, bitmap, checkBoxAutoFixCommonErrors.Checked, checkBoxPromptForUnknownWords.Checked, true, checkBoxGuessUnknownWords.Checked);
                    }
                    else 
                    { // fix some error manually (modi not available)
                        line = _ocrFixEngine.FixUnknownWordsViaGuessOrPrompt(out wordsNotFound, line, index, bitmap, checkBoxAutoFixCommonErrors.Checked, checkBoxPromptForUnknownWords.Checked, true, checkBoxGuessUnknownWords.Checked);
                    }
                }

                if (_ocrFixEngine.Abort)
                {
                    ButtonStopClick(null, null);
                    _ocrFixEngine.Abort = false;
                    return string.Empty;
                }

                // Log used word guesses (via word replace list)
                foreach (string guess in _ocrFixEngine.AutoGuessesUsed)
                    listBoxLogSuggestions.Items.Add(guess);
                _ocrFixEngine.AutoGuessesUsed.Clear();

                // Log unkown words guess (found via spelling dictionaries)
                foreach (string unknownWord in _ocrFixEngine.UnknownWordsFound)
                    listBoxUnknownWords.Items.Add(unknownWord);
                _ocrFixEngine.UnknownWordsFound.Clear();

                if (wordsNotFound >= 3)
                    subtitleListView1.SetBackgroundColor(index, Color.Red);
                if (wordsNotFound == 2)
                    subtitleListView1.SetBackgroundColor(index, Color.Orange);
                else if (wordsNotFound == 1)
                    subtitleListView1.SetBackgroundColor(index, Color.Yellow);
                else if (line.Trim().Length == 0)
                    subtitleListView1.SetBackgroundColor(index, Color.Orange);
                else
                    subtitleListView1.SetBackgroundColor(index, Color.Green);
            }
            else
            { // no dictionary :(
                if (checkBoxAutoFixCommonErrors.Checked)
                    line = _ocrFixEngine.FixOcrErrors(line, index, _lastLine, true, checkBoxGuessUnknownWords.Checked);

                if (badWords >= numberOfWords) //result.Count)
                    subtitleListView1.SetBackgroundColor(index, Color.Red);
                else if (badWords >= numberOfWords / 2) // result.Count / 2)
                    subtitleListView1.SetBackgroundColor(index, Color.Orange);
                else if (badWords > 0)
                    subtitleListView1.SetBackgroundColor(index, Color.Yellow);
                else if (line.Trim().Length == 0)
                    subtitleListView1.SetBackgroundColor(index, Color.Orange);
                else
                    subtitleListView1.SetBackgroundColor(index, Color.Green);
            }
           
            if (textWithOutFixes.ToString().Trim() != line.Trim())
            {
                _tessnetOcrAutoFixes++;
                labelFixesMade.Text = string.Format(" - {0}", _tessnetOcrAutoFixes);
                LogOcrFix(index, textWithOutFixes.ToString(), line);
            }

            if (_vobSubMergedPackist != null)
                bitmap.Dispose();

            return line;
        }

        private string TesseractResizeAndRetry(Bitmap bitmap)
        {
            string result = Tesseract3DoOcrViaExe(ResizeBitmap(bitmap, bitmap.Width * 3, bitmap.Height * 2), _languageId);
            if (result.ToString().Trim().Length == 0)
                result = Tesseract3DoOcrViaExe(ResizeBitmap(bitmap, bitmap.Width * 4, bitmap.Height * 2), _languageId);
            return result.TrimEnd();
        }

        private void LogOcrFix(int index, string oldLine, string newLine)
        {
            listBoxLog.Items.Add(string.Format("#{0}: {1} -> {2}", index+1, oldLine.Replace(Environment.NewLine, " "), newLine.Replace(Environment.NewLine, " ")));
        }

        private string CallModi(int i)
        {
            var bmp = GetSubtitleBitmap(i).Clone() as Bitmap;
            var mp = new ModiParameter { Bitmap = bmp, Text = "", Language = GetModiLanguage() };

            // We call in a seperate thread... or app will crash sometimes :(
            var modiThread = new System.Threading.Thread(DoWork);
            modiThread.Start(mp);
            modiThread.Join(3000); // wait max 3 seconds     
            modiThread.Abort();

            if (!string.IsNullOrEmpty(mp.Text) && mp.Text.Length > 3 && mp.Text.EndsWith(";0]"))
                mp.Text = mp.Text.Substring(0, mp.Text.Length - 3);

            // Try to avoid blank lines by resizing image
            if (string.IsNullOrEmpty(mp.Text))
            {
                bmp = ResizeBitmap(bmp, (int)(bmp.Width * 1.2), (int)(bmp.Height * 1.2));
                mp = new ModiParameter { Bitmap = bmp, Text = "", Language = GetModiLanguage() };

                // We call in a seperate thread... or app will crash sometimes :(
                modiThread = new System.Threading.Thread(DoWork);
                modiThread.Start(mp);
                modiThread.Join(3000); // wait max 3 seconds     
                modiThread.Abort();
            }
            if (string.IsNullOrEmpty(mp.Text))
            {
                bmp = ResizeBitmap(bmp, (int)(bmp.Width * 1.3), (int)(bmp.Height * 1.4)); // a bit scaling
                mp = new ModiParameter { Bitmap = bmp, Text = "", Language = GetModiLanguage() };

                // We call in a seperate thread... or app will crash sometimes :(
                modiThread = new System.Threading.Thread(DoWork);
                modiThread.Start(mp);
                modiThread.Join(3000); // wait max 3 seconds     
                modiThread.Abort();
            }

            return mp.Text;
        }

        public static void DoWork(object data)
        {
            var paramter = (ModiParameter)data;
            string fileName = Path.GetTempPath() + Path.DirectorySeparatorChar + Guid.NewGuid() + ".bmp";
            Object ocrResult = null;
            try
            {
                paramter.Bitmap.Save(fileName);

                Type modiDocType = Type.GetTypeFromProgID("MODI.Document");
                Object modiDoc = Activator.CreateInstance(modiDocType);
                modiDocType.InvokeMember("Create", BindingFlags.InvokeMethod, null, modiDoc, new Object[] { fileName });

                modiDocType.InvokeMember("OCR", BindingFlags.InvokeMethod, null, modiDoc, new Object[] { paramter.Language, true, true });

                Object images = modiDocType.InvokeMember("Images", BindingFlags.GetProperty, null, modiDoc, new Object[] { });
                Type imagesType = images.GetType();

                Object item = imagesType.InvokeMember("Item", BindingFlags.GetProperty, null, images, new Object[] { "0" });
                Type itemType = item.GetType();

                Object layout = itemType.InvokeMember("Layout", BindingFlags.GetProperty, null, item, new Object[] { });
                Type layoutType = layout.GetType();
                ocrResult = layoutType.InvokeMember("Text", BindingFlags.GetProperty, null, layout, new Object[] { });

                modiDocType.InvokeMember("Close", BindingFlags.InvokeMethod, null, modiDoc, new Object[] { false });
            }
            catch
            {
                paramter.Text = string.Empty;
            }

            try
            {
                File.Delete(fileName);
            }
            catch
            {
            }
            if (ocrResult != null)
                paramter.Text = ocrResult.ToString().Trim();
        }

        private void InitializeModi()
        {
            _modiEnabled = false;
            checkBoxUseModiInTesseractForUnknownWords.Enabled = false;
            comboBoxModiLanguage.Enabled = false;
            try
            {
                InitializeModiLanguages();

                _modiType = Type.GetTypeFromProgID("MODI.Document");
                _modiDoc = Activator.CreateInstance(_modiType);

                _modiEnabled = _modiDoc != null;
                comboBoxModiLanguage.Enabled = _modiEnabled;
                checkBoxUseModiInTesseractForUnknownWords.Enabled = _modiEnabled;
            }
            catch
            {
                _modiEnabled = false;
            }
            if (!_modiEnabled && comboBoxOcrMethod.Items.Count == 3)
                comboBoxOcrMethod.Items.RemoveAt(2);
        }

        private void InitializeTesseract()
        {
            string dir = Configuration.TesseractDataFolder;
            if (Directory.Exists(dir))
            {
                var list = new List<string>();
                comboBoxTesseractLanguages.Items.Clear();
                foreach (var culture in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures))
                {
                    string tesseractName = culture.ThreeLetterISOLanguageName;
                    if (culture.LCID == 0x4 && !File.Exists(dir + Path.DirectorySeparatorChar + tesseractName + ".traineddata"))
                        tesseractName = "chi_sim";
                    string trainDataFileName = dir + Path.DirectorySeparatorChar + tesseractName + ".traineddata";
                    if (!list.Contains(culture.ThreeLetterISOLanguageName) && File.Exists(trainDataFileName))
                    {
                        list.Add(culture.ThreeLetterISOLanguageName);
                        comboBoxTesseractLanguages.Items.Add(new TesseractLanguage { Id = tesseractName, Text = culture.EnglishName });
                    }
                }
            }
            if (comboBoxTesseractLanguages.Items.Count > 0)
            {
                for (int i = 0; i < comboBoxTesseractLanguages.Items.Count; i++)
                {
                    if ((comboBoxTesseractLanguages.Items[i] as TesseractLanguage).Id == Configuration.Settings.VobSubOcr.TesseractLastLanguage)
                        comboBoxTesseractLanguages.SelectedIndex = i;
                }

                if (comboBoxTesseractLanguages.SelectedIndex == -1)
                    comboBoxTesseractLanguages.SelectedIndex = 0;
            }
        }

        private void InitializeModiLanguages()
        {
            foreach (ModiLanguage ml in ModiLanguage.AllLanguages)
            {
                comboBoxModiLanguage.Items.Add(ml);
                if (ml.Id == _vobSubOcrSettings.LastModiLanguageId)
                    comboBoxModiLanguage.SelectedIndex = comboBoxModiLanguage.Items.Count - 1;
            }
        }

        private int GetModiLanguage()
        {
            if (comboBoxModiLanguage.SelectedIndex < 0)
                return ModiLanguage.DefaultLanguageId;

            return ((ModiLanguage)comboBoxModiLanguage.SelectedItem).Id;
        }

        private void ButtonStopClick(object sender, EventArgs e)
        {
            _abort = true;
            buttonStop.Enabled = false;
            Application.DoEvents();
            progressBar1.Visible = false;
            labelStatus.Text = string.Empty;
        }

        private void SubtitleListView1SelectedIndexChanged(object sender, EventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count > 0)
            {
                _selectedIndex = subtitleListView1.SelectedItems[0].Index;                
                textBoxCurrentText.Text = _subtitle.Paragraphs[_selectedIndex].Text;
                ShowSubtitleImage(subtitleListView1.SelectedItems[0].Index);
                numericUpDownStartNumber.Value = _selectedIndex + 1;
            }
            else
            {
                _selectedIndex = -1;
                textBoxCurrentText.Text = string.Empty;
            }          
        }

        private void TextBoxCurrentTextTextChanged(object sender, EventArgs e)
        {
            if (_selectedIndex >= 0)
            {
                string text = textBoxCurrentText.Text.TrimEnd();
                _subtitle.Paragraphs[_selectedIndex].Text = text;
                subtitleListView1.SetText(_selectedIndex, text);
            }
        }

        private void ButtonNewCharacterDatabaseClick(object sender, EventArgs e)
        {
            var newFolder = new VobSubOcrNewFolder();
            if (newFolder.ShowDialog(this) == DialogResult.OK)
            {
                _vobSubOcrSettings.LastImageCompareFolder = newFolder.FolderName;
                LoadImageCompareCharacterDatabaseList();
            }
        }

        private void ComboBoxCharacterDatabaseSelectedIndexChanged(object sender, EventArgs e)
        {
            LoadImageCompareBitmaps();
            _vobSubOcrSettings.LastImageCompareFolder = comboBoxCharacterDatabase.SelectedItem.ToString();
        }

        private void ComboBoxModiLanguageSelectedIndexChanged(object sender, EventArgs e)
        {
            _vobSubOcrSettings.LastModiLanguageId = GetModiLanguage();
        }

        private void ButtonEditCharacterDatabaseClick(object sender, EventArgs e)
        {
            EditImageCompareCharacters(null, null);
        }

        public DialogResult EditImageCompareCharacters(string name, string text)
        {
            var formVobSubEditCharacters = new VobSubEditCharacters(comboBoxCharacterDatabase.SelectedItem.ToString(), null);
            formVobSubEditCharacters.Initialize(name, text);

            DialogResult result = formVobSubEditCharacters.ShowDialog();
            if (result == DialogResult.OK)
            {
                _compareDoc = formVobSubEditCharacters.ImageCompareDocument;
                string path = Configuration.VobSubCompareFolder + comboBoxCharacterDatabase.SelectedItem + Path.DirectorySeparatorChar;
                _compareDoc.Save(path + "CompareDescription.xml");
            }
            LoadImageCompareBitmaps();
            return result;
        }

        private void VobSubOcr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                Utilities.ShowHelp("#importvobsub");
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down && e.Modifiers == Keys.Alt)
            {
                int selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex++;
                }
                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
            }
            else if (e.KeyCode == Keys.Up && e.Modifiers == Keys.Alt)
            {
                int selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex--;
                }
                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.G)
            {
                var goToLine = new GoToLine();
                goToLine.Initialize(1, subtitleListView1.Items.Count);
                if (goToLine.ShowDialog(this) == DialogResult.OK)
                {
                    subtitleListView1.SelectNone();
                    subtitleListView1.Items[goToLine.LineNumber - 1].Selected = true;
                    subtitleListView1.Items[goToLine.LineNumber - 1].EnsureVisible();
                    subtitleListView1.Items[goToLine.LineNumber - 1].Focused = true;
                }
            }
        }

        private void ComboBoxTesseractLanguagesSelectedIndexChanged(object sender, EventArgs e)
        {
            Configuration.Settings.VobSubOcr.TesseractLastLanguage = (comboBoxTesseractLanguages.SelectedItem as TesseractLanguage).Id;
            _ocrFixEngine = null;
            LoadOcrFixEngine();
        }

        private void LoadOcrFixEngine()
        {
            if (comboBoxTesseractLanguages.SelectedItem != null)
                _languageId = (comboBoxTesseractLanguages.SelectedItem as TesseractLanguage).Id;
            _ocrFixEngine = new OcrFixEngine(_languageId, this);
            if (_ocrFixEngine.IsDictionaryLoaded)
            {
                string loadedDictionaryName = _ocrFixEngine.SpellCheckDictionaryName;
                int i = 0;
                comboBoxDictionaries.SelectedIndexChanged -= comboBoxDictionaries_SelectedIndexChanged;
                foreach (string item in comboBoxDictionaries.Items)
                {
                    if (item.Contains("[" + loadedDictionaryName + "]"))
                        comboBoxDictionaries.SelectedIndex = i;
                    i++;
                }
                comboBoxDictionaries.SelectedIndexChanged += comboBoxDictionaries_SelectedIndexChanged;
                comboBoxDictionaries.Left = labelDictionaryLoaded.Left + labelDictionaryLoaded.Width;
                comboBoxDictionaries.Width = groupBoxOcrAutoFix.Width - (comboBoxDictionaries.Left + 5);
            }
            else
            {
                comboBoxDictionaries.SelectedIndex = 0;
            }

            if (_modiEnabled && checkBoxUseModiInTesseractForUnknownWords.Checked)
            {
                string tesseractLanguageText = (comboBoxTesseractLanguages.SelectedItem as TesseractLanguage).Text;
                int i = 0;
                foreach (var modiLanguage in comboBoxModiLanguage.Items)
                {
                    if ((modiLanguage as ModiLanguage).Text == tesseractLanguageText)
                        comboBoxModiLanguage.SelectedIndex = i;
                    i++;
                }
            }
            comboBoxModiLanguage.SelectedIndex = -1;
        }

        private void ComboBoxOcrMethodSelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxOcrMethod.SelectedIndex == 0)
            {
                ShowOcrMethodGroupBox(GroupBoxTesseractMethod);
                Configuration.Settings.VobSubOcr.LastOcrMethod = "Tesseract";
            }
            else if (comboBoxOcrMethod.SelectedIndex == 1)
            {
                ShowOcrMethodGroupBox(groupBoxImageCompareMethod);
                Configuration.Settings.VobSubOcr.LastOcrMethod = "BitmapCompare";
                checkBoxPromptForUnknownWords.Checked = false;
            }
            else if (comboBoxOcrMethod.SelectedIndex == 2)
            {
                ShowOcrMethodGroupBox(groupBoxModiMethod);
                Configuration.Settings.VobSubOcr.LastOcrMethod = "MODI";
            }
        }

        private void ShowOcrMethodGroupBox(GroupBox groupBox)
        {
            GroupBoxTesseractMethod.Visible = false;
            groupBoxImageCompareMethod.Visible = false;
            groupBoxModiMethod.Visible = false;

            groupBox.Visible = true;
            groupBox.BringToFront();
            groupBox.Left = comboBoxOcrMethod.Left;
            groupBox.Top = 50;
        }

        private void ListBoxLogSelectedIndexChanged(object sender, EventArgs e)
        {
            var lb = sender as ListBox;
            if (lb != null &&  lb.SelectedIndex >= 0)
            {
                string text = lb.Items[lb.SelectedIndex].ToString();
                if (text.Contains(":"))
                {
                    string number = text.Substring(1, text.IndexOf(":") - 1);
                    subtitleListView1.SelectIndexAndEnsureVisible(int.Parse(number)-1);
                }
            }
        }

        private void ContextMenuStripListviewOpening(object sender, CancelEventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count == 0)
                e.Cancel = true;

            if (contextMenuStripListview.SourceControl == subtitleListView1)
            {
                normalToolStripMenuItem.Visible = true;
                italicToolStripMenuItem.Visible = true;
                toolStripSeparator1.Visible = true;
                toolStripSeparator1.Visible = subtitleListView1.SelectedItems.Count == 1;
                saveImageAsToolStripMenuItem.Visible = subtitleListView1.SelectedItems.Count == 1;
            }
            else
            {
                normalToolStripMenuItem.Visible = false;
                italicToolStripMenuItem.Visible = false;
                toolStripSeparator1.Visible = false;
                saveImageAsToolStripMenuItem.Visible = true;
            }

            if (comboBoxOcrMethod.SelectedIndex == 1) // image compare
            {
                toolStripSeparatorImageCompare.Visible = true;
                inspectImageCompareMatchesForCurrentImageToolStripMenuItem.Visible = true;
                EditLastAdditionsToolStripMenuItem.Visible = _lastAdditions != null && _lastAdditions.Count > 0;
            }
            else
            {
                toolStripSeparatorImageCompare.Visible = false;
                inspectImageCompareMatchesForCurrentImageToolStripMenuItem.Visible = false;
                EditLastAdditionsToolStripMenuItem.Visible = false;
            }
        }

        private void SaveImageAsToolStripMenuItemClick(object sender, EventArgs e)
        {
            saveFileDialog1.Title = Configuration.Settings.Language.VobSubOcr.SaveSubtitleImageAs;
            saveFileDialog1.AddExtension = true;                
            saveFileDialog1.FileName = "Image" + _selectedIndex;
            saveFileDialog1.Filter = "PNG image|*.png|BMP image|*.bmp|GIF image|*.gif|TIFF image|*.tiff";
            saveFileDialog1.FilterIndex = 0;

            DialogResult result = saveFileDialog1.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                Bitmap bmp = GetSubtitleBitmap(_selectedIndex);
                if (bmp == null)
                {
                    MessageBox.Show("No image!");
                    return;
                }

                try
                {
                    if (saveFileDialog1.FilterIndex == 0)
                        bmp.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    else if (saveFileDialog1.FilterIndex == 1)
                        bmp.Save(saveFileDialog1.FileName);
                    else if (saveFileDialog1.FilterIndex == 2)
                        bmp.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Gif);
                    else
                        bmp.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Tiff);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                }
            }
        }

        private void saveAllImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog(this) == DialogResult.OK)
            {
                const int height = 1080;
                const int width = 1920;
                const int border = 25;
                int imagesSavedCount = 0;
                StringBuilder sb = new StringBuilder();
                progressBar1.Maximum = _subtitle.Paragraphs.Count - 1;
                progressBar1.Value = 0;
                progressBar1.Visible = true;
                for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
                {
                    progressBar1.Value = i;
                    Bitmap bmp = GetSubtitleBitmap(i);
                    string numberString = string.Format("{0:0000}", i + 1);
                    if (bmp != null)
                    {

                        string fileName = Path.Combine(folderBrowserDialog1.SelectedPath, numberString + ".png");
                        bmp.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                        imagesSavedCount++;

                        Paragraph p = _subtitle.Paragraphs[i];
                        //<Event InTC="00:00:24:07" OutTC="00:00:31:13" Forced="False">
                        //  <Graphic Width="696" Height="111" X="612" Y="930">subtitle_exp_0001.png</Graphic>
                        //</Event>
                        sb.AppendLine("<Event InTC=\"" + BdnXmlTimeCode(p.StartTime) + "\" OutTC=\"" + BdnXmlTimeCode(p.EndTime) + "\" Forced=\"False\">");
                        int x = (width - bmp.Width) / 2;
                        int y = height - (bmp.Height + border);
                        sb.AppendLine("  <Graphic Width=\"" + bmp.Width.ToString() + "\" Height=\"" + bmp.Height.ToString() + "\" X=\"" + x.ToString() + "\" Y=\"" + y.ToString() + "\">" + numberString + ".png</Graphic>");
                        sb.AppendLine("</Event>");

                        bmp.Dispose();
                    }
                }
                XmlDocument doc = new XmlDocument();
                Paragraph first = _subtitle.Paragraphs[0];
                Paragraph last = _subtitle.Paragraphs[_subtitle.Paragraphs.Count - 1];
                doc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine +
                            "<BDN Version=\"0.93\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"BD-03-006-0093b BDN File Format.xsd\">" + Environment.NewLine +
                            "<Description>" + Environment.NewLine +
                            "<Name Title=\"subtitle_exp\" Content=\"\"/>" + Environment.NewLine +
                            "<Language Code=\"eng\"/>" + Environment.NewLine +
                            "<Format VideoFormat=\"1080p\" FrameRate=\"25\" DropFrame=\"False\"/>" + Environment.NewLine +
                            "<Events Type=\"Graphic\" FirstEventInTC=\"" + BdnXmlTimeCode(first.StartTime) + "\" LastEventOutTC=\"" + BdnXmlTimeCode(last.EndTime) + "\" NumberofEvents=\"" + imagesSavedCount.ToString() + "\"/>" + Environment.NewLine +
                            "</Description>" + Environment.NewLine +
                            "<Events>" + Environment.NewLine +
                            "</Events>" + Environment.NewLine +
                            "</BDN>");
                XmlNode events = doc.DocumentElement.SelectSingleNode("Events");
                events.InnerXml = sb.ToString();

                File.WriteAllText(Path.Combine(folderBrowserDialog1.SelectedPath, "BDN_Index.xml"), doc.OuterXml);
                progressBar1.Visible = false;
                MessageBox.Show(string.Format("{0} images saved in {1}", imagesSavedCount, folderBrowserDialog1.SelectedPath));
            }
        }

        private static string BdnXmlTimeCode(TimeCode timecode)
        {
            int frames = timecode.Milliseconds / 40; // 40==25fps (1000/25)
            return string.Format("{0:00}:{1:00}:{2:00}:{3:00}", timecode.Hours, timecode.Minutes, timecode.Seconds, frames);
        }

        private void NormalToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && subtitleListView1.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in subtitleListView1.SelectedItems)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                    if (p != null)
                    {
                        p.Text = Utilities.RemoveHtmlTags(p.Text);
                        subtitleListView1.SetText(item.Index, p.Text);
                        textBoxCurrentText.Text = p.Text;
                    }
                }
            }
        }

        private void ItalicToolStripMenuItemClick(object sender, EventArgs e)
        {
            const string tag = "i";
            if (_subtitle.Paragraphs.Count > 0 && subtitleListView1.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in subtitleListView1.SelectedItems)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                    if (p != null)
                    {
                        if (p.Text.Contains("<" + tag + ">"))
                        {
                            p.Text = p.Text.Replace("<" + tag + ">", string.Empty);
                            p.Text = p.Text.Replace("</" + tag + ">", string.Empty);
                        }
                        p.Text = string.Format("<{0}>{1}</{0}>", tag, p.Text);
                        subtitleListView1.SetText(item.Index, p.Text);
                        textBoxCurrentText.Text = p.Text;
                    }
                }
            }
        }

        private void CheckBoxCustomFourColorsCheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxCustomFourColors.Checked)
            {
                pictureBoxPattern.BackColor = Color.White;
                pictureBoxEmphasis1.BackColor = Color.Black;
                pictureBoxEmphasis2.BackColor = Color.Black;
                checkBoxBackgroundTransparent.Enabled = true;
                checkBoxPatternTransparent.Enabled = true;
                checkBoxEmphasis1Transparent.Enabled = true;
                checkBoxEmphasis2Transparent.Enabled = true;
            }
            else
            {
                pictureBoxPattern.BackColor = Color.Gray;
                pictureBoxEmphasis1.BackColor = Color.Gray;
                pictureBoxEmphasis2.BackColor = Color.Gray;
                checkBoxBackgroundTransparent.Enabled = false;
                checkBoxPatternTransparent.Enabled = false;
                checkBoxEmphasis1Transparent.Enabled = false;
                checkBoxEmphasis2Transparent.Enabled = false;
            }
            SubtitleListView1SelectedIndexChanged(null, null);
        }

        private void PictureBoxColorChooserClick(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog(this) == DialogResult.OK)
                (sender as PictureBox).BackColor = colorDialog1.Color;
            SubtitleListView1SelectedIndexChanged(null, null);
        }

        private void CheckBoxPatternTransparentCheckedChanged(object sender, EventArgs e)
        {
            SubtitleListView1SelectedIndexChanged(null, null);
        }

        private void CheckBoxEmphasis1TransparentCheckedChanged(object sender, EventArgs e)
        {
            SubtitleListView1SelectedIndexChanged(null, null);
        }

        private void CheckBoxEmphasis2TransparentCheckedChanged(object sender, EventArgs e)
        {
            SubtitleListView1SelectedIndexChanged(null, null);
        }

        private void checkBoxShowOnlyForced_CheckedChanged(object sender, EventArgs e)
        {
            Subtitle oldSubtitle = new Subtitle(_subtitle);
            subtitleListView1.BeginUpdate();
            if (_bdnXmlOriginal != null)
                LoadBdnXml();
            else if (_bluRaySubtitlesOriginal != null)
                LoadBluRaySup();
            else
                LoadVobRip();
            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                Paragraph current = _subtitle.Paragraphs[i];
                foreach (Paragraph old in oldSubtitle.Paragraphs)
                {
                    if (current.StartTime.TotalMilliseconds == old.StartTime.TotalMilliseconds &&
                        current.Duration.TotalMilliseconds == old.Duration.TotalMilliseconds)
                    {
                        current.Text = old.Text;
                        break;
                    }

                }
            }
            subtitleListView1.Fill(_subtitle);
            subtitleListView1.EndUpdate();
        }
    
        private void checkBoxUseTimeCodesFromIdx_CheckedChanged(object sender, EventArgs e)
        {
            Subtitle oldSubtitle = new Subtitle(_subtitle);
            subtitleListView1.BeginUpdate();
            LoadVobRip();
            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                Paragraph p = oldSubtitle.GetParagraphOrDefault(i);
                if (p != null && p.Text != string.Empty)
                {
                    _subtitle.Paragraphs[i].Text = p.Text;                    
                }
            }
            subtitleListView1.Fill(_subtitle);
            subtitleListView1.EndUpdate();
        }

        public string LanguageString
        {
            get
            {
                string name = comboBoxDictionaries.SelectedItem.ToString();
                int start = name.LastIndexOf("[");
                int end = name.LastIndexOf("]");
                if (start > 0 && end > start)
                {
                    start++;
                    name = name.Substring(start, end - start);
                    return name;
                }
                return null;
            }
        }

        private void comboBoxDictionaries_SelectedIndexChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.SpellCheckLanguage = LanguageString;
            if (_ocrFixEngine != null && LanguageString != null)
                _ocrFixEngine.SpellCheckDictionaryName = LanguageString;
        }

        internal void Initialize(Subtitle bdnSubtitle, VobSubOcrSettings vobSubOcrSettings)
        {
            _bdnXmlOriginal = bdnSubtitle;
            _bdnFileName = bdnSubtitle.FileName;

            buttonOK.Enabled = false;
            buttonCancel.Enabled = false;
            buttonStartOcr.Enabled = false;
            buttonStop.Enabled = false;
            buttonNewCharacterDatabase.Enabled = false;
            buttonEditCharacterDatabase.Enabled = false;
            labelStatus.Text = string.Empty;
            progressBar1.Visible = false;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            numericUpDownPixelsIsSpace.Value = 11; // vobSubOcrSettings.XOrMorePixelsMakesSpace;
            _vobSubOcrSettings = vobSubOcrSettings;

            InitializeModi();
            InitializeTesseract();
            LoadImageCompareCharacterDatabaseList();

            if (Configuration.Settings.VobSubOcr.LastOcrMethod == "BitmapCompare" && comboBoxOcrMethod.Items.Count > 1)
                comboBoxOcrMethod.SelectedIndex = 1;
            else if (Configuration.Settings.VobSubOcr.LastOcrMethod == "MODI" && comboBoxOcrMethod.Items.Count > 2)
                comboBoxOcrMethod.SelectedIndex = 2;
            else
                comboBoxOcrMethod.SelectedIndex = 0;
          
            groupBoxImagePalette.Visible = false;

            Text = Configuration.Settings.Language.VobSubOcr.TitleBluRay;
            Text += " - " + Path.GetFileName(_bdnFileName);

            checkBoxAutoTransparentBackground.Checked = true;
        }

        internal void StartOcrFromDelayed()
        {
            if (_lastAdditions.Count > 0)
            {
                var last = _lastAdditions[_lastAdditions.Count - 1]; 
                numericUpDownStartNumber.Value = last.Index + 1;
                Timer t = new Timer();
                t.Interval = 200;
                t.Tick += new EventHandler(t_Tick);
                t.Start();
            }
        }

        void t_Tick(object sender, EventArgs e)
        {
            (sender as Timer).Stop();
            ButtonStartOcrClick(null, null);
        }

        private void VobSubOcr_Resize(object sender, EventArgs e)
        {
            int originalTopHeight = 105;

            int adjustPercent = (int)(Height * 0.15);
            groupBoxSubtitleImage.Height = originalTopHeight + adjustPercent;
            groupBoxOcrMethod.Height = groupBoxSubtitleImage.Height;

            groupBoxOcrAutoFix.Top = groupBoxSubtitleImage.Top + groupBoxSubtitleImage.Height + 5;
            labelSubtitleText.Top = groupBoxOcrAutoFix.Top;
            subtitleListView1.Top = labelSubtitleText.Top + labelSubtitleText.Height + 3;
            subtitleListView1.Height = textBoxCurrentText.Top - subtitleListView1.Top - 3;
            groupBoxOcrAutoFix.Height = buttonOK.Top - groupBoxOcrAutoFix.Top - 3;
        }

        private void importTextWithMatchingTimeCodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = Configuration.Settings.Language.General.OpenSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                string fileName = openFileDialog1.FileName;
                if (!File.Exists(fileName))
                    return;

                var fi = new FileInfo(fileName);
                if (fi.Length > 1024 * 1024 * 10) // max 10 mb
                {
                    if (MessageBox.Show(string.Format(Configuration.Settings.Language.Main.FileXIsLargerThan10Mb + Environment.NewLine +
                                                      Environment.NewLine +
                                                      Configuration.Settings.Language.Main.ContinueAnyway,
                                                      fileName), Text, MessageBoxButtons.YesNoCancel) != DialogResult.Yes)
                        return;
                }

                Subtitle sub = new Subtitle();
                Encoding encoding = null;
                SubtitleFormat format = sub.LoadSubtitle(fileName, out encoding, encoding);
                if (format == null || sub == null)
                    return;

                int index = 0;
                foreach (Paragraph p in sub.Paragraphs)
                {
                    foreach (Paragraph currentP in _subtitle.Paragraphs)
                    {
                        if (string.IsNullOrEmpty(currentP.Text) && p.StartTime.TotalMilliseconds == currentP.StartTime.TotalMilliseconds)
                        {
                            currentP.Text = p.Text;
                            subtitleListView1.SetText(index, p.Text);
                            break;
                        }
                    }
                    index++;
                }
                
            }
        }

        private void saveAllImagesWithHtmlIndexViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog(this) == DialogResult.OK)
            {
                progressBar1.Maximum = _subtitle.Paragraphs.Count-1;
                progressBar1.Value = 0;
                progressBar1.Visible = true;
                int imagesSavedCount = 0;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<html>");
                sb.AppendLine("<head><title>Subtitle images</title></head>");
                sb.AppendLine("<body>");
                for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
                {
                    progressBar1.Value = i;
                    Bitmap bmp = GetSubtitleBitmap(i);
                    string numberString = string.Format("{0:0000}", i + 1);
                    if (bmp != null)
                    {
                        string fileName = Path.Combine(folderBrowserDialog1.SelectedPath, numberString + ".png");
                        bmp.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                        imagesSavedCount++;
                        Paragraph p = _subtitle.Paragraphs[i];
                        sb.AppendLine(string.Format("#{3}: {0}->{1}<div style='text-align:center'><img src='{2}.png' /></div><br /><hr />", p.StartTime.ToShortString(), p.EndTime.ToShortString(), numberString, i));
                        bmp.Dispose();
                    }
                }
                sb.AppendLine("</body>");
                sb.AppendLine("</html>");
                string htmlFileName = Path.Combine(folderBrowserDialog1.SelectedPath, "index.html");
                File.WriteAllText(htmlFileName, sb.ToString());
                progressBar1.Visible = false;
                MessageBox.Show(string.Format("{0} images saved in {1}", imagesSavedCount, folderBrowserDialog1.SelectedPath));
                Process.Start(htmlFileName);
            }
        }

        private void inspectImageCompareMatchesForCurrentImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count != 1)
                return;

            Cursor = Cursors.WaitCursor;
            Bitmap bitmap = GetSubtitleBitmap(subtitleListView1.SelectedItems[0].Index);
            var matches = new List<CompareMatch>();
            List<ImageSplitterItem> list = ImageSplitter.SplitBitmapToLetters(bitmap, (int)numericUpDownPixelsIsSpace.Value, checkBoxRightToLeft.Checked, Configuration.Settings.VobSubOcr.TopToBottom);
            int index = 0;
            var imageSources = new List<Bitmap>();
            while (index < list.Count)
            {
                ImageSplitterItem item = list[index];
                if (item.Bitmap == null)
                {
                    matches.Add(new CompareMatch(item.SpecialCharacter, false, 0, null));
                    imageSources.Add(null);
                }
                else
                {
                    CompareMatch bestGuess;
                    CompareMatch match = GetCompareMatch(item, bitmap, out bestGuess);
                    if (match == null)
                    {
                        matches.Add(new CompareMatch(Configuration.Settings.Language.VobSubOcr.NoMatch, false, 0, null));
                        imageSources.Add(item.Bitmap);
                    }
                    else // found image match
                    {
                        matches.Add(new CompareMatch(match.Text, match.Italic, 0, match.Name));
                        imageSources.Add(item.Bitmap);
                        if (match.ExpandCount > 0)
                            index += match.ExpandCount - 1;
                    }
                }
                index++;
            }
            Cursor = Cursors.Default;
            VobSubOcrCharacterInspect inspect = new VobSubOcrCharacterInspect();
            inspect.Initialize(comboBoxCharacterDatabase.SelectedItem.ToString(), matches, imageSources);
            if (inspect.ShowDialog(this) == DialogResult.OK)
            {
                _compareDoc = inspect.ImageCompareDocument;
                string path = Configuration.VobSubCompareFolder + comboBoxCharacterDatabase.SelectedItem + Path.DirectorySeparatorChar;
                _compareDoc.Save(path + "CompareDescription.xml");
            }
        }

        private void inspectLastAdditionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VobSubEditCharacters formVobSubEditCharacters = new VobSubEditCharacters(comboBoxCharacterDatabase.SelectedItem.ToString(), _lastAdditions);
            if (formVobSubEditCharacters.ShowDialog(this) == DialogResult.OK)
            {
                _lastAdditions = formVobSubEditCharacters.Additions;
                _compareDoc = formVobSubEditCharacters.ImageCompareDocument;
                string path = Configuration.VobSubCompareFolder + comboBoxCharacterDatabase.SelectedItem + Path.DirectorySeparatorChar;
                _compareDoc.Save(path + "CompareDescription.xml");
            }
            LoadImageCompareBitmaps();
        }

        private void checkBoxAutoTransparentBackground_CheckedChanged(object sender, EventArgs e)
        {
            SubtitleListView1SelectedIndexChanged(null, null);
        }

    }
}
