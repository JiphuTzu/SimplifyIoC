/*
 * @class SimplifyIoC.Framework.SemiBinding
 *
 * 一个受管理的值列表。
 *
 * @see SimplifyIoC.Framework.ISemiBinding
 */

namespace SimplifyIoC.Framework
{
    public class SemiBinding : ISemiBinding
    {
        protected object[] objectValue;

        public BindingConstraintType constraint { get; set; }
        public bool uniqueValues { get; set; }

        public SemiBinding()
        {
            constraint = BindingConstraintType.One;
            uniqueValues = true;
        }

        #region IManagedList implementation

        public IManagedList Add(object o)
        {
            if (objectValue == null || constraint == BindingConstraintType.One)
            {
                objectValue = new object[1];
            }
            else
            {
                if (uniqueValues)
                {
                    var aa = objectValue.Length;
                    for (var a = 0; a < aa; a++)
                    {
                        var val = objectValue[a];
                        if (val.Equals(o))
                        {
                            return this;
                        }
                    }
                }

                var tempList = objectValue;
                var len = tempList.Length;
                objectValue = new object[len + 1];
                tempList.CopyTo(objectValue, 0);
            }
            objectValue[objectValue.Length - 1] = o;

            return this;
        }

        public IManagedList Add(object[] list)
        {
            foreach (var item in list)
                Add(item);

            return this;
        }

        public IManagedList Remove(object o)
        {
            //P0#9 修复：原来先 o.Equals(objectValue)（与内部数组比引用，无意义且 o 为 null 时 NRE）
            if (objectValue == null) return this;
            var aa = objectValue.Length;
            for (var a = 0; a < aa; a++)
            {
                var currVal = objectValue[a];
                var matched = (o == null) ? (currVal == null) : o.Equals(currVal);
                if (matched)
                {
                    SpliceValueAt(a);
                    return this;
                }
            }
            return this;
        }

        public IManagedList Remove(object[] list)
        {
            foreach (var item in list)
                Remove(item);

            return this;
        }
        public virtual object value
        {
            get
            {
                if (constraint.Equals(BindingConstraintType.One))
                {
                    return objectValue?[0];
                }
                return objectValue;
            }
        }

        #endregion

        /// Remove the value at index splicePos
        protected void SpliceValueAt(int splicePos)
        {
            var newList = new object[objectValue.Length - 1];
            var mod = 0;
            var aa = objectValue.Length;
            for (var a = 0; a < aa; a++)
            {
                if (a == splicePos)
                {
                    mod = -1;
                    continue;
                }
                newList[a + mod] = objectValue[a];
            }
            objectValue = newList.Length == 0 ? null : newList;
        }
    }
}