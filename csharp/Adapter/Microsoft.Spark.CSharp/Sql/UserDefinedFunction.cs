using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Spark.CSharp.Core;
using Microsoft.Spark.CSharp.Proxy;
using Microsoft.Spark.CSharp.Interop;

namespace Microsoft.Spark.CSharp.Sql
{
    public class UserDefinedFunction<RT>
    {
        private readonly IUDFProxy udfProxy;

        public UserDefinedFunction(Func<int, IEnumerable<dynamic>, IEnumerable<dynamic>> func)
        {
            udfProxy = SparkCLREnvironment.SparkCLRProxy.SparkContextProxy.CreateUserDefinedCSharpFunction(
                func.GetType().Name,
                SparkContext.BuildCommand(new CSharpWorkerFunc(func), SerializedMode.Row, SerializedMode.Row),
                Functions.GetReturnType(typeof(RT)));
        }

        public UserDefinedFunction(IUDFProxy udfProxy)
        {
            this.udfProxy = udfProxy;
        }

        private Column Execute(params Column[] columns)
        {
            return new Column(udfProxy.Apply(columns.Select(c => c.ColumnProxy).ToArray()));
        }

        #region udf Execute() overloads to support up to 0 - 10 input Column argument(s)
        public Column Execute0()
        {
            return Execute();
        }

        public Column Execute1(Column c1)
        {
            return Execute(c1);
        }

        public Column Execute2(Column c1, Column c2)
        {
            return Execute(c1, c2);
        }

        public Column Execute3(Column c1, Column c2, Column c3)
        {
            return Execute(c1, c2, c3);
        }

        public Column Execute4(Column c1, Column c2, Column c3, Column c4)
        {
            return Execute(c1, c2, c3, c4);
        }

        public Column Execute5(Column c1, Column c2, Column c3, Column c4, Column c5)
        {
            return Execute(c1, c2, c3, c4, c5);
        }

        public Column Execute6(Column c1, Column c2, Column c3, Column c4, Column c5, Column c6)
        {
            return Execute(c1, c2, c3, c4, c5, c6);
        }

        public Column Execute7(Column c1, Column c2, Column c3, Column c4, Column c5, Column c6, Column c7)
        {
            return Execute(c1, c2, c3, c4, c5, c6, c7);
        }

        public Column Execute8(Column c1, Column c2, Column c3, Column c4, Column c5, Column c6, Column c7, Column c8)
        {
            return Execute(c1, c2, c3, c4, c5, c6, c7, c8);
        }

        public Column Execute9(Column c1, Column c2, Column c3, Column c4, Column c5, Column c6, Column c7, Column c8, Column c9)
        {
            return Execute(c1, c2, c3, c4, c5, c6, c7, c8, c9);
        }

        public Column Execute10(Column c1, Column c2, Column c3, Column c4, Column c5, Column c6, Column c7, Column c8, Column c9, Column c10)
        {
            return Execute(c1, c2, c3, c4, c5, c6, c7, c8, c9, c10);
        }
        #endregion
    }
}
