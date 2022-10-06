using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if RFEM
using ModelClient = Dlubal.WS.Rfem6.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rfem6.Model.object_types;
#elif RSTAB
using ModelClient = Dlubal.WS.Rstab9.Model.RfemModelClient;
using ObjectTypes = Dlubal.WS.Rstab9.Model.object_types;
#endif

namespace Dlubal
{
    public class DlubalBaseObject : IComparable<DlubalBaseObject>
    {
        /// <summary>
        /// Value of UserId must be greater than 0 and also have to be unique for all nodes in Model.
        /// </summary>
        public int UserId
        {
            get => userIdField;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("UserId must be greater than 0");
                }
                userIdField = value;
            }
        }

        public static ObjectTypes DlubalObjectType => ObjectTypes.E_OBJECT_TYPE_NOTE;

        public virtual string ObjectName => "BaseObject";
        public virtual string CollectionName => $"{ObjectName}s";

        private int userIdField;

        protected IMainApp? MainApp { get; set; }

        public int CompareTo(DlubalBaseObject? other)
        {
            if (other == null)
            {
                return 1;
            }

            return UserId.CompareTo(other.UserId);
        }

        protected virtual IComparer<DlubalBaseObject> GetComparer()
        {
            return new DlubalBaseObjectComparer();
        }
    }

    public class DlubalBaseObjectComparer : IComparer<DlubalBaseObject>, IEqualityComparer<DlubalBaseObject>
    {
        public int Compare(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            return x.CompareTo(y);
        }

        public bool Equals(DlubalBaseObject? x, DlubalBaseObject? y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null)
            {
                return false;
            }

            return x.Equals(y);
        }

        public int GetHashCode([DisallowNull] DlubalBaseObject obj)
        {
            return obj.UserId.GetHashCode();
        }
    }
}
