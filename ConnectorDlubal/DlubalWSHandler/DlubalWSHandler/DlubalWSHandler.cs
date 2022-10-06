using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceModel;

#if RFEM
using ApplicationClient = Dlubal.WS.Rfem6.Application.RfemApplicationClient;
using ModelClient = Dlubal.WS.Rfem6.Model.RfemModelClient;

#elif RSTAB
using ApplicationClient = Dlubal.WS.Rstab9.Application.RstabApplicationClient;
using ModelClient = Dlubal.WS.Rstab9.Model.RfemModelClient;
#endif

namespace Dlubal
{
    /// <summary>
    /// This class provide all of the handling around DlubalWS.
    /// All operations with takes long time, should be done asynchronously.
    /// Handling of these operation should by done in object that implements IMainApp. 
    /// </summary>
    public class DlubalWSHandler : INotifyPropertyChanged
    {
#if RFEM
        public const string ApplicationName = "RFEM6";
        private const int PortNumber = 8081;
        private const string AppFolder = "RFEM";
#elif RSTAB
        public const string ApplicationName = "RSTAB9";
        private static readonly int PortNumber = 8091;
        private cosnt string AppFolder = "RSTAB";
#endif

        private readonly EndpointAddress Address = new(string.Format("http://localhost:{0}", PortNumber.ToString()));
        private IMainApp? MainApp = null;
        private ApplicationClient? ApplicationClient;

        public ObservableCollection<string> ModelNames { get; private set; }

        public DlubalWSHandler(IMainApp? mainApp)
        {
            MainApp = mainApp;
            ModelNames = new ObservableCollection<string>();
        }

        public enum ModelSelection
        {
            New,
            Active,
            ActiveOrNew,
            SelectByName
        }

        private static BasicHttpBinding Binding
        {
            get
            {
                BasicHttpBinding binding = new BasicHttpBinding
                {
                    // Send timeout is set to 60 seconds.
                    SendTimeout = new TimeSpan(0, 0, 60),
                    UseDefaultWebProxy = true,
                };

                return binding;
            }
        }

        /// <summary>
        /// Long operation should be executed in separately in different threads then MainApp. 
        /// Also we should be able to executed in same order.
        /// In this queue are stored waiting tasks to execution.
        /// </summary>
        private Queue<Task> Tasks = new();

        /// <summary>
        /// If Tasks queue is empty, add the task to queue and start routine that runs all tasks in queue in order.
        /// </summary>
        /// <param name="task"></param>
        private void AddTask(Task task)
        {
            if (!Tasks.Any())
            {
                Tasks.Enqueue(task);
                Task.Run(() =>
                {
                    while (Tasks.Any())
                    {
                        Task currentTask = Tasks.Dequeue();
                        currentTask.Start();
                        currentTask.Wait();
                    }
                });
            }
            else
            {
                Tasks.Enqueue(task);
            }
        }

        public bool IsDlubalApplicationRunning()
        {
            Process[] process = Process.GetProcessesByName(ApplicationName);
            return process.Length > 0;
        }

        /// <summary>
        /// Method will looks throw registry and try to find and run rfem6/rstab9 app. Works only on computer with instaled rfem6/rstab9 application.
        /// </summary>
        /// <returns></returns>
        public bool StartApplication()
        {
            if (!IsDlubalApplicationRunning())
            {
                try
                {
                    string exePath = GetPathOfDlubalApplication(ApplicationName);
                    if (exePath == null) return false;

                    Process.Start(exePath, string.Format("--start-soap-server {0}", PortNumber.ToString()));
                }
                catch (Exception ex)
                {
                    MainApp?.Log(IMainApp.ErrorMessageType, string.Format("Opening {0} application failed. {1}", ApplicationName, ex.Message));
                }
            }
            return true;
        }

        /// <summary>
        /// Close the connected application through web services
        /// </summary>
        /// <returns></returns>
        public bool CloseApplication()
        {
            if (IsConnected())
            {
                ApplicationClient?.close_application();
                return true;
            }
            return false;
        }

        private bool LoadModelNames(bool logNoModelsWarning = true)
        {
            if (!IsConnected())
            {
                return false;
            }

            try
            {
                string[]? modelsList = ApplicationClient?.get_model_list();
                if (modelsList == null) return false;

                foreach (string model in modelsList)
                {
                    ModelNames.Add(model);
                }

                if (modelsList.Length == 0)
                {
                    if (logNoModelsWarning)
                    {
                        MainApp?.Log(IMainApp.InfoMessageType, "No models found.");
                    }
                }

            }
            catch (Exception ex)
            {
                MainApp?.Log(IMainApp.ErrorMessageType, "Loading of RFEM models failed. " + ex.Message);
                return false;
            }

            return ModelNames.Any();
        }

        public void Disconnect()
        {
            if (IsConnected())
            {
                if (ApplicationClient?.State != CommunicationState.Faulted)
                {
                    ApplicationClient?.Close();
                }
                else
                {
                    ApplicationClient.Abort();
                }
                ApplicationClient = null;
            }
            ModelNames.Clear();
        }

        private bool IsConnected()
        {
            return ApplicationClient != null && ApplicationClient.State != CommunicationState.Closed;
        }

        public bool Connect()
        {
            try
            {
                ApplicationClient = new ApplicationClient(Binding, Address);
            }
            catch (Exception ex)
            {
                MainApp?.Log(IMainApp.ErrorMessageType, ex.Message);
                Disconnect();
                return false;
            }

            LoadModelNames(false);
            return true;
        }

        private string GetPathOfDlubalApplication(string applicationName)
        {
            using (RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey installRoot = localMachine.OpenSubKey(string.Format(@"SOFTWARE\DLUBAL\{0}", AppFolder), false))
            {
                if (installRoot != null)
                {
                    //Do something with installRoot
                    string[] appVersion = installRoot.GetSubKeyNames();
                    string newestVersion = appVersion.Max();
                    using (RegistryKey versionFolder = installRoot.OpenSubKey(newestVersion, false))
                    {
                        string exePath = versionFolder?.GetValue("InstallDirectory") as string;
                        return exePath == null ? null : string.Format(@"{0}\bin\{1}.exe", exePath, applicationName);
                    }
                }
            }
            return "";
        }

        public ModelHandler? GetModel(ModelSelection selection, string modelName = "")
        {
            try
            {
                ModelClient modelClient = OpenModel(selection, modelName);
                return new(modelClient, this, MainApp);
            } 
            catch (Exception ex)
            {
                MainApp?.Log(IMainApp.ErrorMessageType, ex.Message);
                return null;
            }
        }
        /// <summary>
        /// Return true if originName is different from versionedName only by numbers at end.
        /// </summary>
        /// <param name="originName"></param>
        /// <param name="versionedName"></param>
        /// <returns></returns>
        private static bool IsSameWithoutVersion(string originName, string versionedName)
        {
            if (originName == null || versionedName == null)
            {
                return false;
            }

            if (originName.Length > versionedName.Length)
            {
                return false;
            }

            for (int i = 0; i < originName.Length; i++)
            {
                if (originName[i] != versionedName[i])
                {
                    return false;
                }
            }

            for (int i = originName.Length; i < versionedName.Length; i++)
            {
                if (!Char.IsDigit(versionedName[i]))
                {
                    return false;
                }
            }
            return true;
        }

        internal ModelClient OpenModel (ModelSelection selection, string modelName = "")
        {
            if (ApplicationClient == null)
            {
                throw new InvalidOperationException("Application is not connected!");
            }

            string modelUrl = "";
            switch (selection)
            {
                case ModelSelection.New:
                    return CreateNewModel(modelName);

                case ModelSelection.Active:
                    modelUrl = ApplicationClient.get_active_model();
                    if (modelUrl is null || modelUrl == "")
                    {
                        throw new ArgumentException("In " + ApplicationName + "no active model found.");
                    }
                    break;
                case ModelSelection.ActiveOrNew:
                    
                    if (!ModelNames.Any())
                    {
                        return CreateNewModel(modelName);
                    }

                    modelUrl = ApplicationClient.get_active_model();
                    break;

                case ModelSelection.SelectByName:
                    List<string> modelNames = ModelNames.ToList();

                    int index = modelNames.IndexOf(modelName);
                    if (index < 0)
                    {
                        throw new ArgumentException(ApplicationName + " doesn't not contains model with name " + modelName);
                    }

                    modelUrl = ApplicationClient.get_model(index);
                    break;
            }

            ModelClient result = new ModelClient(Binding, new EndpointAddress(modelUrl));
            result.Open();
            return result;
        }

        private ModelClient CreateNewModel(string modelName)
        {
            try
            {
                if (ApplicationClient is null)
                {
                    throw new Exception("Application client is null");
                }

                string[] modelNames = ApplicationClient.get_model_list();
                int modelWithSameName = 0;
                foreach (string name in modelNames)
                {
                    modelWithSameName += IsSameWithoutVersion(modelName, name) ? 1 : 0;
                }

                if (modelWithSameName != 0)
                {
                    while (modelName.Contains(String.Format("{0}{1}", modelName, modelWithSameName)))
                    {
                        modelWithSameName++;
                    }
                }
                string modelNameWithVersion = modelWithSameName == 0
                                                ? modelName
                                                : String.Format("{0}{1}", modelName, modelWithSameName);

                string url = ApplicationClient.new_model(modelNameWithVersion);
                ModelClient modelClient = new(Binding, new EndpointAddress(url));
                modelClient.reset();
                return modelClient;
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region INotifyImplementation
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyImplementation
    }
}