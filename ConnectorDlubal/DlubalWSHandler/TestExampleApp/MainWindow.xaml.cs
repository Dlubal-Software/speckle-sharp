using Dlubal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestExampleApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMainApp
    {
        public MainWindow()
        {
            InitializeComponent();
            DlubalWSHandler handler = new(this);

            if (!handler.IsDlubalApplicationRunning())
            {
                handler.StartApplication();
            }

            handler.Connect();

            ModelHandler? modelHandler = handler.GetModel(DlubalWSHandler.ModelSelection.New, "New Model");

            var getRandomDouble = () =>
            {
                return (-1 * Random.Shared.NextInt64() % 2) * 10 * Random.Shared.NextDouble();
            };
            for (int i = 0; i < 20; i++)
            {
                Node node = new(getRandomDouble(), getRandomDouble(), getRandomDouble());
                modelHandler?.AddNodeToCache(node);
            }

            modelHandler?.WriteNodeCacheToDlubalApplication();

            modelHandler?.LoadNodesToCache();
            if (modelHandler == null) return;

            foreach (var tuple in modelHandler.Nodes)
            {
                ContentLabel.Content += string.Format("ID: {0}; ({1} , {2} , {3} )\n", tuple.UserId, tuple.X, tuple.Y, tuple.Z);
            }

            handler.CloseApplication();
        }

        public void Log(string messageType, string message)
        {
            Dispatcher.Invoke(() => MessageBox.Show(this, message, messageType, MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }
}
