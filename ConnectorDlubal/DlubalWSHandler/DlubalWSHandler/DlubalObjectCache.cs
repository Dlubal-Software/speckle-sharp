using Dlubal;
using Dlubal.WS.Rfem6.Model;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// This class provide basic functionality around importing and exporting objects data from/to ModelClient object.
    /// Cache also handle providing a unique UserId to object and basic consolidation (with a dependency on well-implemented comparer for DlubalBaseObject child class.
    /// </summary>
    internal class DlubalObjectCache
    {
        private Func<int, ModelHandler, DlubalBaseObject> GetObjectFromModel;
        private Func<DlubalBaseObject, ModelHandler, bool> WriteObjectToModel;

        private SortedSet<DlubalBaseObject> cache;
        private ObjectTypes storedObjectType;
        private bool isLoaded = false;
        int lastObjectId = 0;

        internal ModelHandler ModelHandler { get; set; }

        public IMainApp? MainApp { get; set; }

        /// <summary>
        /// Create Cache object for storing of DlubalBaseObject.
        /// </summary>
        /// <param name="comparer">Comparer for object</param>
        /// <param name="storedObjectType">Dlubal type (from low level WS library) of stored object</param>
        /// <param name="modelClient">Model client from rstab/rfem</param>
        /// <param name="getObjectFunction">Function for getting low level object and converting it to hight level object from modelClient.</param>
        /// <param name="writeObjectFunction">Function for writing and converting high level object to lower level object and send it into rfem/rstab.</param>
        internal DlubalObjectCache(IComparer<DlubalBaseObject> comparer, ObjectTypes storedObjectType, ModelHandler model, Func<int, ModelHandler, DlubalBaseObject> getObjectFunction, Func<DlubalBaseObject, ModelHandler, bool> writeObjectFunction)
        {
            cache = new SortedSet<DlubalBaseObject>(comparer);
            this.storedObjectType = storedObjectType;
            ModelHandler = model;
            GetObjectFromModel = getObjectFunction;
            WriteObjectToModel = writeObjectFunction;
        }

        /// <summary>
        /// If cache is not loaded, load object to cache and return highest UserId.
        /// If cache is loaded, just return higher UserId of objects in cache.
        /// </summary>
        /// <returns></returns>
        internal int GetLastUserId()
        {
            if (isLoaded)
            {
                return lastObjectId;
            }

            LoadObjects();
            return lastObjectId;
        }

        /// <summary>
        /// If is same object in cache already return false,
        /// otherwise add obj to cache and return true.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal bool AddObject (DlubalBaseObject obj)
        {
            if (!isLoaded)
            {
                LoadObjects();
            }

            if (cache.Contains(obj))
            {
                return false;
            }

            obj.UserId = ++lastObjectId;
            cache.Add(obj);
            return true;
        }

        /// <summary>
        /// Clear cache and load objects from Model to cache.
        /// </summary>
        /// <returns></returns>
        internal bool LoadObjects()
        {
            ModelClient? model = ModelHandler.ModelClient;
            if (model == null) return false;
            Clear();

            int objectCount = model.get_object_count(storedObjectType, 0);
            lastObjectId = objectCount;
            if (objectCount == 0)
            {
                return false;
            }

            for (int i = 1; i <= objectCount; i++)
            {
                DlubalBaseObject obj = GetObjectFromModel(i, ModelHandler);
                if (obj == null)
                {
                    continue;
                }

                if (obj.UserId > lastObjectId)
                {
                    lastObjectId = obj.UserId;
                }

                cache.Add(obj);
            }

            isLoaded = true;
            return true;
        }

        internal void Clear()
        {
            cache.Clear();
            lastObjectId = 0;
            isLoaded = false;
        }

        /// <summary>
        /// Looking for object with specific userId in cache, if is not exist, returns null.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        internal DlubalBaseObject? GetObject(int userId)
        {
            if (!isLoaded)
            {
                LoadObjects();
            }

            foreach (DlubalBaseObject obj in cache)
            {
                if (obj.UserId == userId) return obj;
            }
            return null;
        }

        /// <summary>
        /// Writes objects from cache to Dlubal App.
        /// Functionality is dependent on implementation of writeObjectFunstion from class constructions.
        /// </summary>
        /// <returns></returns>
        internal bool WriteObjects()
        {
            ModelClient? model = ModelHandler.ModelClient;

            if (!cache.Any() || model == null)
            {
                return false;
            }

            bool result = true;
            foreach (DlubalBaseObject obj in cache)
            {
                result &= WriteObjectToModel(obj, ModelHandler);
            }

            return result;
        }

        /// <summary>
        /// Don't add instance to this list directly!! Use AddObject method instead!
        /// </summary>
        internal SortedSet<DlubalBaseObject> Objects
        {
            get => cache;
        }

        internal static Node GetNode(int UserId, ModelHandler model)
        {
            ModelClient? client = model.ModelClient;
            if (client == null)
            {
                return null;
            }

            Node result = new Node(client.get_node(UserId));
            return result;
        }

        internal static bool WriteNode(DlubalBaseObject obj, ModelHandler model)
        {
            ModelClient? client = model.ModelClient;
            if (client == null || obj is not Node node)
            {
                return false;
            }

            client.set_node(node.GetDlubalNode());
            return true;
        }

        internal static Line GetLine(int userId, ModelHandler handler)
        {
            ModelClient? client = handler.ModelClient;
            if (client == null)
            {
                return null;
            }

            line dlubalLine = client.get_line(userId);
            if (dlubalLine == null)
            {
                return null;
            }

            switch (dlubalLine.type)
            {
                case line_type.TYPE_POLYLINE:
                    int[] nodeIds = dlubalLine.definition_nodes;
                    List<Node> nodes = new List<Node>();

                    // Get nodes data from model handler.
                    foreach (int nodeId in nodeIds)
                    {
                        DlubalBaseObject? obj = handler.GetObjectFromCache(nodeId, Node.DlubalObjectType);
                        if (obj == null)
                        {
                            handler.App?.Log(IMainApp.ErrorMessageType, "GetLine: obj is null.");
                            continue;
                        }

                        if (obj is Node node)
                        {
                            nodes.Add(node);
                        }
                        else
                        {
                            throw new Exception("Obj is not instance of Node.");
                        }
                    }
                    Line result = new Line(nodes, userId, handler.App);
                    return result;
                default:
                    return null;
            }
        }

        internal static bool WriteLine(DlubalBaseObject obj, ModelHandler handler)
        {
            if (obj is not Line line || handler.ModelClient is null)
            {
                return false;
            }

            line dlubalLine = line.GetDlubalLine();
            handler.ModelClient.set_line(dlubalLine);
            return true;
        }
    }
}
