using Dlubal;
using Objects.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Line = Dlubal.Line;

namespace Objects.Converter.DLUBAL
{
    public class ConverterDlubal : ISpeckleConverter, IMainApp
    {
        private ModelHandler? modelHandler;

        public static readonly string ServicedAppName = "DLUBAL";
        public string Description => "Default Speckle Kit for Rfem6 and Rstab9";

        public string Name => ServicedAppName;

        public string Author => "Dlubal Software s.r.o.";

        public string WebsiteOrEmail => "https://www.dlubal.com/";

        public ProgressReport Report { get; private set; } = new ProgressReport();

        public ReceiveMode ReceiveMode { get; set; }

        public bool CanConvertToNative(Base @object)
        {
            return @object switch
            {
                Point => true,
                Polyline => true,
                _ => false,
            };
        }

        public bool CanConvertToSpeckle(object @object)
        {
            return true;
        }

        public object ConvertToNative(Base @object)
        {
            object dlubalObject = null;
            switch (@object)
            {
                case Point o:
                    dlubalObject = PointToNative(o);
                    Report.Log($"Created Point {o.id}");
                    break;

                case Polyline line:
                    dlubalObject = LineToNative(line);
                    Report.Log($"Created Polyline {line.id}");
                    break;

                default:
                    return new object();
            }

            return dlubalObject;
        }

        public List<object> ConvertToNative(List<Base> objects)
        {
            List<object> result = new List<object>();
            foreach (Base obj in objects)
            {
                result.Add(ConvertToNative(obj));
            }

            return result;
        }

        public Base ConvertToSpeckle(object @object)
        {
            switch (@object)
            {
                case Dlubal.Node o:
                    return PointToSpeckle(o);
                default:
                    return null;
                    break;
            }
        }

        public List<Base> ConvertToSpeckle(List<object> objects)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetServicedApplications()
        {
            return new[] { ServicedAppName };
        }

        public void SetContextDocument(object doc)
        {
            if (doc is ModelHandler)
            {
                modelHandler = (ModelHandler)doc;
            }
        }


        public void SetContextObjects(List<ApplicationObject> objects)
        {
            throw new NotImplementedException();
        }

        public void SetConverterSettings(object settings)
        {
            throw new NotImplementedException();
        }

        public void SetPreviousContextObjects(List<ApplicationObject> objects)
        {
            throw new NotImplementedException();
        }


        #region Object convert methods
        private object PointToNative(Point o)
        {
            Node node = new(o.x, o.y, o.z);
            modelHandler?.AddObjectToCache(node);

            return node;
        }

        private Point PointToSpeckle(Node o)
        {
            Point result = new Point();
            result.x = o.X;
            result.y = o.Y;
            result.z = o.Z;

            return result;
        }

        private object LineToNative(Polyline line)
        {
            if (modelHandler == null)
            {
                Report.LogOperationError(new Exception("Model Client is not connected."));
                return null;
            }

            Node[] nodes = new Node[line.GetPoints().Count];
            var specklePoints = line.GetPoints();

            for (int i = 0; i < line.GetPoints().Count; i++)
            {
                object point = PointToNative(specklePoints[i]);
                if (point is Node)
                {
                    nodes[i] = (Node)point;

                }
                else
                {
                    Report.LogConversionError(new Exception($"Conversion of {specklePoints[i].id} point in {line.id} line failed."));
                }
            }

            Line result = new Line(nodes, 1, this);
            modelHandler?.AddObjectToCache(result);

            return result;
        }

        public void Log(string messageType, string message)
        {
            throw new NotImplementedException();
        }

        #endregion Object convert methods
    }
}