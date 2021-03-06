﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AKCCore;


namespace AKCWPF {
    /// <summary>
    /// Interaction logic for MainWindow.xaml. This window lets the user select their clipping file language,
    /// providing the GUI to fire off the parserController class, which handles parsing. 
    /// </summary>

    public partial class MainWindow : Window {

        public ParserController parserController;
        private Encoding encoding; //Using UTF8 encoding by default here as defined in OptionsDeprecate, but that can be changed.
        private string textSample;
        private string textPreview; //Text preview gets up to n lines, as defined in var maxLineCounter.
        private string defaultDirectory; //Variables to keep track of the directory in which the .txt are.
        private string lastUsedDirectory;

        private LoadingWindow LW;

        public MainWindow() {
            parserController = new ParserController();

            //GUI simple options and persistence
            encoding = parserController.options.FileEncoding;
            defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            lastUsedDirectory = null;
            parserController.options.Language = "NotALanguage";

            InitializeComponent();
        }

        private void BrowseFile() {
            /// <summary>
            /// Browsing folders to find formats, different options depending on current culture.
            /// </summary>

            // A) Fire off OFD, configure depending on culture.
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".txt";
            ofd.Filter = "TXT Files (*.txt)|*.txt";

            //Check culture, set up default file names accordingly. 
            if (parserController.options.SelectedCulture.Name == ("en-GB")) {
                ofd.FileName = "My Clippings - Kindle.txt"; 
            } else {
                ofd.FileName = "Mis recortes.txt"; 
            }

            //Get initial directory. 
            if (String.IsNullOrEmpty(lastUsedDirectory)) {
                ofd.InitialDirectory = defaultDirectory;
            } else {
                ofd.InitialDirectory = lastUsedDirectory;
            }

            if (ofd.ShowDialog() == true) {
                try {
                    string filePath = ofd.FileName;
                    string safeFilePath = ofd.SafeFileName;

                    textPreview = parserController.GeneratePreviewFromPath(filePath);

                    string detectedLanguage = parserController.DetectLanguageFromPreview(textPreview);

                    try {
                        parserController.options.Language = detectedLanguage;

                        if (detectedLanguage == "Spanish") {
                            radioButtonB.IsChecked = true;
                        }

                        if (detectedLanguage == "English") { 
                            radioButtonA.IsChecked = true;
                        }
                    } catch (Exception ex) {
                        MessageBox.Show(ex.Message, "Unable to complete language check.");
                    }

                    pathBox.Text = filePath; //Updates path in path textbox.
                    lastUsedDirectory = filePath; //Remembers last used directory for user convenience.
                    filePreview.Text = textPreview; //Updates preview of the file in text block.

                    parserController.options.TextToParsePath = filePath; //References preview in general text to parse.
                    previewScroll.UpdateLayout();

                } catch (IOException) {
                    MessageBox.Show("Sorry, file is not valid.");
                }
            }
        }
        
        private void browseButton_Click(object sender, RoutedEventArgs e) {
            BrowseFile();
        }

        private async void Parse() {
            if (parserController.options.TextToParsePath != null && parserController.options.Language != null) {

                bool correctParserConfirmed = parserController.ConfirmParserCompatibility(textSample, textPreview);

                try {
                    if (correctParserConfirmed == false) {
                        MessageBoxResult parsingProblemMessageBox = MessageBox.Show("Potential language incompatibilities detected. Are you sure you want to continue? \r\n \r\n Click 'Cancel' to go back and select the correct language (RECOMMENDED) or 'OK' to continue (WARNING: program might became inestable or crash.)",
                            "Parsing problem?", System.Windows.MessageBoxButton.OKCancel, MessageBoxImage.Information, MessageBoxResult.Cancel);
                        if (parsingProblemMessageBox == MessageBoxResult.OK) {
                            correctParserConfirmed = true;
                        }
                    }
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Parsing problem");
                }

                if (correctParserConfirmed) {

                    //Async parsing
                    LW = new LoadingWindow();
                    await Task.Run(() => parserController.RunParser(parserController.options.TextToParsePath));
                    LW.CloseLoadingWindow();

                    //Result generation
                    dynamic result = parserController.ReportParsingResult(false);

                    if (result != null) {
                        MessageBox.Show(result.clippingCount + " clippings parsed.", "Parsing successful.");
                        MessageBox.Show(result.databaseEntries.ToString() + " clippings added to database. " +
                        result.removedClippings.ToString() + " empty or null clippings removed.", "Database created.");
                        if (result.databaseEntries <= 0) {
                            MessageBox.Show("No clippings added to database, please try again with a different file.");
                        } else {
                            //If you want to update UI from this task a dispatcher has to be used, since it has to be in the UI thread.
                            Dispatcher.Invoke((Action)delegate () {
                                LaunchDatabaseWindow();
                            });
                        }
                    } else {
                        MessageBox.Show("Parsing failed");
                    }
                }
            }

            if (parserController.options.TextToParsePath == null) {
                MessageBox.Show("No path to .txt found, please select your Kindle clipping file and try again.");
            }

            if (parserController.options.Language == null) {
                MessageBox.Show("Problems detecting language, please select your language and try again.");
            }
        }

        private void buttonParse_Click(object sender, RoutedEventArgs e) {
            Parse();
        }
            

        private void radioButtonA_Checked(object sender, RoutedEventArgs e) {
            parserController.options.Language = "English";
            parserController.options.SelectedCulture = parserController.options.EngCulture;
        }

        private void radioButtonB_Checked(object sender, RoutedEventArgs e) {
            //radioButtonB.IsChecked = true;
            parserController.options.Language = "Spanish";
            parserController.options.SelectedCulture = parserController.options.SpaCulture;
        }

        private void LaunchDatabaseWindow() {
            var databaseWindow = new DatabaseWindow { Owner = this };
            databaseWindow.ShowDialog();
        }
    }
}