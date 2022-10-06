using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if RFEM
using ApplicationClient = Dlubal.WS.Rfem6.Application.RfemApplicationClient;
using ModelClient = Dlubal.WS.Rfem6.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rfem6.Model.object_types;
#elif RSTAB
using ApplicationClient = Dlubal.WS.Rstab9.Application.RstabApplicationClient;
using ModelClient = Dlubal.WS.Rstab9.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rstab9.Model.object_types;
#endif

namespace Dlubal
{
    public class ModelHandler : INotifyPropertyChanged
    {
        private DlubalWSHandler Parent;

        private ModelClient? Model;

        private DlubalObjectCache nodeCache;
        private DlubalObjectCache lineCache;
        private DlubalObjectCache NodeCache { get => nodeCache; set => nodeCache = value; }
        internal DlubalObjectCache LineCache { get => lineCache; set => lineCache = value; }

        private bool IsOpenModification = false;

        public IMainApp? App { get; private set; }

        public static List<ObjectTypes> SupportedObjectTypes => new List<ObjectTypes>()
        {
            ObjectTypes.E_OBJECT_TYPE_NODE,
            ObjectTypes.E_OBJECT_TYPE_LINE
        };

        internal ModelHandler(ModelClient model, DlubalWSHandler parentApplication, IMainApp? mainApp)
        {
            Parent = parentApplication;
            App = mainApp;
            Model = model;

            NodeCache = new DlubalObjectCache(new NodeComparer(), ObjectTypes.E_OBJECT_TYPE_NODE, this, DlubalObjectCache.GetNode, DlubalObjectCache.WriteNode)
            {
                MainApp = mainApp
            };

            LineCache = new DlubalObjectCache(new LineComparer(), Line.DlubalObjectType, this, DlubalObjectCache.GetLine, DlubalObjectCache.WriteLine)
            {
                MainApp = mainApp
            };
        }

        internal ModelClient? ModelClient => Model;

        public string GetModelName()
        {
            if (Model == null)
            {
                return "";
            }

            var modelPar = Model.get_model_main_parameters();
            return modelPar.model_name;
        }

        public string GetModelPath()
        {
            if (Model == null)
            {
                return "";
            }

            return Model.get_model_main_parameters().model_path;
        }

        /// <summary>
        /// Add object to cache (if there is any for object type) and set object UserId to first free UserId in cache.
        /// </summary>
        /// <param name="obj">Object to add.</param>
        public bool AddObjectToCache(DlubalBaseObject obj)
        {
            return obj switch
            {
                null => false,
                Node => NodeCache.AddObject(obj),
                Line => LineCache.AddObject(obj),
                _ => false,
            };
        }

        public DlubalBaseObject? GetObjectFromCache(int userId, ObjectTypes type)
        {
            if (type == Node.DlubalObjectType)
            {
                return nodeCache.GetObject(userId);
            }
            else if (type == Line.DlubalObjectType)
            {
                return lineCache.GetObject(userId);
            }
            return null;
        }

        /// <summary>
        /// Load object from rfem/rstab to cache.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public bool LoadObjectsToCache(ObjectTypes objectType)
        {
            if (objectType == Node.DlubalObjectType)
            {
                return NodeCache.LoadObjects();
            }
            else if (objectType == Line.DlubalObjectType)
            {
                return LineCache.LoadObjects();
            }

            return false;
        }

        public bool WriteCache(IEnumerable<ObjectTypes> objects)
        {
            bool result = true;
            OpenModification("Import of objects");

            foreach (ObjectTypes obj in objects)
            {
                try
                {
                    result &= WriteCache(obj);
                }
                catch (Exception ex)
                {
                    App?.Log(IMainApp.ErrorMessageType, ex.Message);
                }
            }

            CloseModification();
            return result;
        }

        public bool WriteCache(ObjectTypes objectType, bool closeModificationAfterFinnish = true)
        {
            OpenModification($"Import {nameof(objectType)}");

            bool result = false;

            if (objectType == Node.DlubalObjectType)
            {
                result = NodeCache.WriteObjects();
            }
            else if (objectType == Line.DlubalObjectType)
            {
                result = LineCache.WriteObjects();
            }

            if (closeModificationAfterFinnish)
            {
                CloseModification();
            }
            return result;
        }

        public IEnumerable<DlubalBaseObject> GetObjects(ObjectTypes type)
        {
           if (type == Node.DlubalObjectType)
           {
               return NodeCache.Objects;
           }

            App?.Log(IMainApp.InfoMessageType, $"Type {nameof(type)} is not supported.");
            return new List<DlubalBaseObject>();
        }

        private void CloseModification()
        {
            if (Model == null)
            {
                return;
            }

            IsOpenModification = false;
            Model.finish_modification();
        }

        private void OpenModification(string modificationName)
        {
            if (IsOpenModification) return;

            if (Model == null)
            {
                return;
            }

            IsOpenModification = true;
            Model.begin_modification(modificationName);
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
