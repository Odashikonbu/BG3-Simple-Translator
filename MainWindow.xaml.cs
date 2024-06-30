using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using System.IO;
using System.Media;
using Microsoft.Win32;
using System.Windows.Media;

namespace BG3Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Top : Window
    {
        private enum SearchMode
        {
            Translated,
            Origin
        }
        private Stack<XmlItem[]> undoStack = new Stack<XmlItem[]>();
        private Stack<XmlItem[]> redoStack = new Stack<XmlItem[]>();
        private SearchMode currentMode = SearchMode.Translated; public ICommand FullScreenCommand { get; set; }
        public ICommand MinimizeCommand { get; set; }
        private string filePath;

        private ObservableCollection<XmlItem> xmlItems;
        public Top()
        {
            InitializeComponent();
            this.KeyDown += Window_KeyDown;
        }

        private void InitializeDataGrid()
        {
            xmlItems = new ObservableCollection<XmlItem>();
            WorkArea.ItemsSource = xmlItems;
        }
        private void OpenXmlFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML Files (*.xml)|*.xml";
            if (openFileDialog.ShowDialog() == true)
            {
                filePath = openFileDialog.FileName;
                try
                {
                    InitializeDataGrid(); 
                    List<XmlItem> items = LoadDataFromXml(filePath);
                    foreach (var item in items)
                    {
                        xmlItems.Add(item);
                    }
                    this.Title = "BG3Translator - " + Path.GetFileName(filePath);


                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading XML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Mouse.OverrideCursor = Cursors.Hand;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private void Window_Drop(object sender, DragEventArgs e)
        {
            Mouse.OverrideCursor = null;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    filePath = files[0];
                    if (Path.GetExtension(filePath).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {

                        InitializeDataGrid();
                        List<XmlItem> items = LoadDataFromXml(filePath);
                        foreach (var item in items)
                        {
                            xmlItems.Add(item);
                        }
                        this.Title = "BG3Translator - " + Path.GetFileName(filePath);
                    }
                    else
                    {
                        MessageBox.Show("Invalid File Type");
                    }
                }
            }
        }
        private List<XmlItem> LoadDataFromXml(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found.");
            }

            if (Path.GetExtension(filePath).ToLower() != ".xml")
            {
                throw new FileLoadException("File format is not supported.");
            }

            try
            {
                XDocument doc = XDocument.Load(filePath);

                List<XmlItem> items = new List<XmlItem>();
                foreach (XElement element in doc.Root.Elements("content"))
                {
                    string uuid = element.Attribute("contentuid")?.Value;
                    string originText = element.Value;
                    string translatedText = originText;

                    items.Add(new XmlItem { UUID = uuid, OriginText = originText, TranslatedText = translatedText });
                }

                return items;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading XML file: {ex.Message}");
            }
        }
        

        private void SaveCommandExecute(object parameter)
        {
            SaveXmlFile();
        }

        private void SaveXmlFile_Click(object sender, RoutedEventArgs e)
        {
            SaveXmlFile();
        }
        private void SaveXmlFile()
        {
            if (filePath != null)
            {
                try
                {
                    XDocument doc = XDocument.Load(filePath);

                    foreach (var item in WorkArea.Items)
                    {
                        if (item is XmlItem dataItem)
                        {
                            XElement contentElement = new XElement("content");
                            contentElement.SetAttributeValue("contentuid", dataItem.UUID);
                            contentElement.SetAttributeValue("version", "1");
                            contentElement.Value = dataItem.TranslatedText;

                            var existingElement = doc.Descendants("content")
                                .FirstOrDefault(e => e.Attribute("contentuid")?.Value == dataItem.UUID);

                            if (existingElement != null)
                            {
                                existingElement.ReplaceWith(contentElement);
                            }
                            else
                            {
                                doc.Root.Add(contentElement);
                            }
                        }
                    }

                    doc.Save(filePath);
                    outputMessage(Path.GetFileName(filePath) + " saved!!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"error occured while xml save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void UpdateSearchMode()
        {
            switch (currentMode)
            {
                case SearchMode.Translated:
                    SearchModeButton.Content = "⇨";
                    break;
                case SearchMode.Origin:
                    SearchModeButton.Content = "⇦";
                    break;
                default:
                    break;
            }
        }
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }
        private void ApplyFilter()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(WorkArea.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    XmlItem XmlItem = item as XmlItem;
                    if (XmlItem != null)
                    {
                        string searchTerm = SearchTextBox.Text.Trim();
                        if (string.IsNullOrEmpty(searchTerm))
                            return true;

                        switch (currentMode)
                        {
                            case SearchMode.Translated:
                                return XmlItem.TranslatedText.Contains(searchTerm);
                            case SearchMode.Origin:
                                return XmlItem.OriginText.Contains(searchTerm);
                            default:
                                return true;
                        }
                    }
                    return true;
                };
            }
        }
        private void SearchModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMode == SearchMode.Translated)
            {
                currentMode = SearchMode.Origin;
            }
            else
            {
                currentMode = SearchMode.Translated;
            }

            UpdateSearchMode();
            ApplyFilter();
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                OpenXmlFile_Click(null, null);
            }
        }
        private void WorkArea_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            SaveEditHistory();

        }
        private void SaveEditHistory()
        {
            XmlItem[] currentData = xmlItems.Select(item => (XmlItem)item.Clone()).ToArray();
            undoStack.Push(currentData);
            redoStack.Clear();
        }
        private void Undo()
        {
            if (undoStack.Count > 0)
            {
                XmlItem[] currentData = xmlItems.Select(item => (XmlItem)item.Clone()).ToArray();
                redoStack.Push(currentData);

                XmlItem[] previousData = undoStack.Pop();
                xmlItems.Clear();
                foreach (var item in previousData)
                {
                    xmlItems.Add(item);
                }
                WorkArea.Items.Refresh();
            }
        }
        private void Redo()
        {
            if (redoStack.Count > 0)
            {
                XmlItem[] currentData = xmlItems.Select(item => (XmlItem)item.Clone()).ToArray();
                undoStack.Push(currentData);

                XmlItem[] nextData = redoStack.Pop();
                xmlItems.Clear();
                foreach (var item in nextData)
                {
                    xmlItems.Add(item);
                }
                WorkArea.Items.Refresh();
            }
        }
        private void UndoMenu_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void RedoMenu_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }
        private void UpdateData(string uuid, XmlItem item)
        {
            var xmlItem = xmlItems.FirstOrDefault(x => x.UUID == uuid);
            if (xmlItem != null)
            {
                xmlItem.OriginText = item.OriginText;
                xmlItem.TranslatedText = item.TranslatedText;

                WorkArea.Items.Refresh();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                Undo();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                Redo();
            }
            else if (e.Key == Key.F11)
            {
                FullScreen();
            }
            else if (e.Key == Key.F12)
            {
                Minimize();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchTextBox.Focus();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                if (!IsAnyCellEditing())
                {
                    SaveXmlFile();
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
            {
                if (!IsAnyCellEditing())
                {
                    SaveXmlFile();
                    Close();
                }
            }
        }
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
        }
        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            FullScreen();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            Minimize();
        }
        private void FullScreen()
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }

        private void Minimize()
        {
            WindowState = WindowState.Minimized;
        }
        private void KeyBind()
        {
            KeyBinding fullScreenKeyBinding = new KeyBinding(FullScreenCommand, new KeyGesture(Key.F11));
            InputBindings.Add(fullScreenKeyBinding);

            KeyBinding minimizeKeyBinding = new KeyBinding(MinimizeCommand, new KeyGesture(Key.M, ModifierKeys.Control));
            InputBindings.Add(minimizeKeyBinding);
        }
        private async void outputMessage(string message)
        {
            System.Media.SystemSounds.Asterisk.Play();
            HelpText.Text= message;
            await Task.Delay(TimeSpan.FromSeconds(5));
            HelpText.Text = string.Empty;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveXmlFile();
            Close();
        }
        private bool IsAnyCellEditing()
        {
            foreach (var cell in WorkArea.SelectedCells)
            {
                // EditingElementを取得して、それがnullでない場合は編集中と見なす
                var content = cell.Column.GetCellContent(cell.Item);
                if (content is TextBox textBox && textBox.IsKeyboardFocused)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth && parameter is string ratioString && double.TryParse(ratioString, out double ratio))
            {
                return actualWidth * ratio / 10;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class XmlItem
    {
        public string UUID { get; set; }
        public string OriginText { get; set; }
        public string TranslatedText { get; set; }
        public XmlItem Clone()
        {
            return new XmlItem
            {
                UUID = this.UUID,
                OriginText = this.OriginText,
                TranslatedText = this.TranslatedText
            };
        }
    }
}