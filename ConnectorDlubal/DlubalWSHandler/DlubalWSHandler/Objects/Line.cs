using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if RFEM
using Dlubal;
using Dlubal.WS.Rfem6.Model;
using ModelClient = Dlubal.WS.Rfem6.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rfem6.Model.object_types;
#elif RSTAB
using ModelClient = Dlubal.WS.Rstab9.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rstab9.Model.object_types;
#endif

namespace Dlubal
{
    public class Line : DlubalBaseObject, IEquatable<DlubalBaseObject>, IComparable<DlubalBaseObject>
    {
        public enum LineType
        {
            Arc,
            Circle,
            CutViaSection,
            CutViaTwoLines,
            Ellipse,
            EllipticalArc,
            Nurbs,
            Parabola,
            Polyline,
            Spline
        };

        public override string ObjectName => "Line";

        public Node[] Points { get; private set; }

        public Node StartPoint
        {
            get => Points[0];
        }

        public Node EndPoint
        {
            get => Points[Points.Length - 1];
        }

        public Line(IEnumerable<Node> points, int userId, IMainApp? mainApp = null)
        {
            UserId = userId;
            MainApp = mainApp;

            Points = new Node[points.Count()];
            int i = 0;
            foreach(Node node in points)
            {
                Points[i++] = node;
            }
        }

        public int CompareTo(Line? other)
        {
            if (other == null) return 1;

            for (int i = 0; i < Points.Length; i++)
            {
                if (i >= other.Points.Length) return 1;
                if (Points[i].CompareTo(other.Points[i]) < 0) return -1;
                if (Points[i].CompareTo(other.Points[i]) > 0) return 1;
            }
            if (Points.Length < other.Points.Length)
                return -1;
              
            return 0;
        }

        public bool Equals(DlubalBaseObject? other)
        {
            if (other == null) return false;

            return other.CompareTo(this) == 0;
        }

        /// <summary>
        /// Create new dlubal polyline.
        /// </summary>
        /// <returns></returns>
        public line GetDlubalLine()
        {
            int[] pointIds = new int[Points.Length];
            for (int i = 0; i < pointIds.Length; i++)
            {
                pointIds[i] = Points[i].UserId;
            }

            line result = new line()
            {
                no = UserId,
                type = line_type.TYPE_POLYLINE,
                typeSpecified = true,
                definition_nodes = pointIds,
            };

            return result;
        }

        protected override IComparer<DlubalBaseObject> GetComparer()
        {
            return new LineComparer();
        }

        public static new object_types DlubalObjectType => object_types.E_OBJECT_TYPE_LINE;
    }

    public class LineComparer : IComparer<DlubalBaseObject>, IEqualityComparer<DlubalBaseObject>
    {
        public int Compare(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x is Line a && y is Line b)
            {
                return a.CompareTo(b);
            }

            if (x is null && y is null) return 0;
            if (x is null) return -1;
            return x.CompareTo(y);
        }

        public bool Equals(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            if (x is Line a && y is Line b) return a.Equals(b);
            return false;
        }

        public int GetHashCode([DisallowNull] DlubalBaseObject obj)
        {
            if (obj is Line a)
            {
                NodeComparer pointComparer = new();
                return pointComparer.GetHashCode(a.StartPoint) * 11 + pointComparer.GetHashCode(a.EndPoint);
            }

            return obj.GetHashCode();
        }
    }
}
