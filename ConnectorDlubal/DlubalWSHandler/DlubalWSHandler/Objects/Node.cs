using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if RFEM
using ApplicationClient = Dlubal.WS.Rfem6.Application.RfemApplicationClient;
using ModelClient = Dlubal.WS.Rfem6.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rfem6.Model.object_types;
using Dlubal.WS.Rfem6.Model;
using coordinate_type = Dlubal.WS.Rfem6.Model.node_coordinate_system_type;

#elif RSTAB
using ApplicationClient = Dlubal.WS.Rstab9.Application.RstabApplicationClient;
using ModelClient = Dlubal.WS.Rstab9.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rstab9.Model.object_types;
using node = Dlubal.WS.Rstab9.Model.node;
using coordinate_type = Dlubal.WS.Rstab9.Model.node_coordinate_system_type;
using Dlubal.Ws.Rstab9.Model;
#endif

namespace Dlubal
{
    /// <summary>
    /// Basic implementation of node in cartesian space.
    /// </summary>
    public class Node : DlubalBaseObject, IComparable<Node>
    {
        private static readonly double TOLERANCE = 0.006; //[m]

        public double X
        { 
            get => vector.x;
            set => vector.x = value;
        }

        public double Y 
        {
            get => vector.y;
            set => vector.y = value;
        }

        public double Z
        {
            get => vector.z;
            set => vector.z = value;
        }

        public override string ObjectName => "Node";

        private vector_3d vector = new();

        public Node(double x, double y, double z, int userId = 1)
        {
            X = x;
            Y = y;
            Z = z;
            UserId = userId;
        }

        public node GetDlubalNode()
        {
            if (UserId <= 0)
            {
                throw new ArgumentException("UserId must be greater then zero.");
            }
            node result = new()
            {
                coordinates = vector,
                coordinate_system_type = coordinate_type.COORDINATE_SYSTEM_CARTESIAN,
                coordinate_system_typeSpecified = true,
                no = UserId
            };

            return result;
        }

        public Node(node dNode)
        {
            if (!dNode.coordinate_3Specified)
            {
                throw new NotImplementedException();
            }

            UserId = dNode.no;

            switch (dNode.coordinate_system_type)
            {
                case coordinate_type.COORDINATE_SYSTEM_CARTESIAN:

                    X = dNode.coordinate_1;
                    Y = dNode.coordinate_2;
                    Z = dNode.coordinate_3;
                    break;

                case coordinate_type.COORDINATE_SYSTEM_Z_CYLINDRICAL:
                    X = dNode.coordinate_1 * Math.Cos(dNode.coordinate_2);
                    Y = dNode.coordinate_1 * Math.Sin(dNode.coordinate_2);
                    Z = dNode.coordinate_3;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public int CompareTo(Node? other)
        {
            if (other == null) return 1;
            if (Math.Abs(other.X - X) < TOLERANCE
                && Math.Abs(other.Y - Y) < TOLERANCE
                && Math.Abs(other.Z - Z) < TOLERANCE)
            {
                return 0;
            }

            if (other.X < X) return 1;
            else if (other.X > X) return -1;
            else if (other.Y < Y) return 1;
            else if (other.Y > Y) return -1;
            else if (other.Z < Z) return 1;
            else if (other.Z > Z) return -1;
            return 0;
        }

        protected override IComparer<DlubalBaseObject> GetComparer()
        {
            return new NodeComparer();
        }

        public static new ObjectTypes DlubalObjectType => ObjectTypes.E_OBJECT_TYPE_NODE;
    }

    public class NodeComparer : IComparer<DlubalBaseObject>, IEqualityComparer<DlubalBaseObject>
    {
        public int Compare(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x is Node a && y is Node b) return a.CompareTo(b);

            return x.CompareTo(y);
        }

        public bool Equals(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            if (x is Node a && y is Node b) return a.Equals(b);

            return x.CompareTo(y) == 0;
        }

        public int GetHashCode([DisallowNull] DlubalBaseObject obj)
        {
            if (obj is Node a)
            {
                return a.X.GetHashCode() * 11 + a.Y.GetHashCode() * 7 + a.Z.GetHashCode();
            }

            return obj.GetHashCode();
        }
    }
}
