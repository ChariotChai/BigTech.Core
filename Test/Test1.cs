using System.Data;
using static Test.DataSet1;

namespace Test
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod1()
        {

            DataSet1 ds1 = new DataSet1();

            ds1.DataTable1.DataTable1RowChanging += DataTable1_DataTable1RowChanging;
            ds1.DataTable1.DataTable1RowChanged += DataTable1_DataTable1RowChanged;
            ds1.DataTable1.DataTable1RowDeleted += DataTable1_DataTable1RowDeleted;
            ds1.DataTable1.DataTable1RowDeleting += DataTable1_DataTable1RowDeleting;

            Console.WriteLine("add case......");
            DataTable1Row r = ds1.DataTable1.NewDataTable1Row();
            r.ID = "a";
            r.Amount = 10;
            Console.WriteLine("add r......");
            ds1.DataTable1.AddDataTable1Row(r);

            DataTable1Row r2 = ds1.DataTable1.NewDataTable1Row();
            r2.ID = "b";
            r2.Amount = 20;
            Console.WriteLine("add r2......");
            ds1.DataTable1.AddDataTable1Row(r2);

            Console.WriteLine("accepting......");
            ds1.AcceptChanges();

            Console.WriteLine("\n");
            Console.WriteLine("delete case......");
            var rr = ds1.DataTable1.Where(ds1 => ds1.ID == "a").Single()!;
            rr.Delete();

            Console.WriteLine("accepting 2......");
            ds1.AcceptChanges();

            Console.WriteLine("\n");
            Console.WriteLine("modify case......");
            var rr2 = ds1.DataTable1.Where(ds1 => ds1.ID == "b").Single()!;
            rr2.Amount += 10;
            Console.WriteLine("accepting3......");
            ds1.AcceptChanges();
        }

        private void DataTable1_DataTable1RowDeleting(object sender, DataTable1RowChangeEvent e)
        {
            Console.WriteLine($"DataTable1_DataTable1RowDeleting action={e.Action} rowState={e.Row.RowState} [{e.Row.ID}]");

        }

        private void DataTable1_DataTable1RowDeleted(object sender, DataTable1RowChangeEvent e)
        {
            Console.WriteLine($"DataTable1_DataTable1RowDeleted action={e.Action} rowState={e.Row.RowState} [{e.Row["ID", DataRowVersion.Original]}]");
        }

        private void DataTable1_DataTable1RowChanged(object sender, DataTable1RowChangeEvent e)
        {
            if (e.Row.RowState == DataRowState.Detached)
            {
                Console.WriteLine($"DataTable1_DataTable1RowChanged action={e.Action} rowState={e.Row.RowState}");
            }
            else
            {
                Console.WriteLine($"DataTable1_DataTable1RowChanged action={e.Action} rowState={e.Row.RowState} [{e.Row.ID}]");
            }
        }

        private void DataTable1_DataTable1RowChanging(object sender, DataTable1RowChangeEvent e)
        {
            if (e.Row.RowState == DataRowState.Added)
            {
                Console.WriteLine($"DataTable1_DataTable1RowChanging action={e.Action} rowState={e.Row.RowState} [{e.Row.ID}]");
            }
            else if (e.Row.RowState == DataRowState.Deleted)
            {
                Console.WriteLine($"DataTable1_DataTable1RowChanging action={e.Action} rowState={e.Row.RowState} [{e.Row["ID", DataRowVersion.Original]}]");
            }
            else
            {
                Console.WriteLine($"DataTable1_DataTable1RowChanging action={e.Action} rowState={e.Row.RowState} [{e.Row.ID}]");
            }
        }
    }
}

// event / action / rowstate 
// changed / add / added -- 新增，未提交
// changing / commit / added -- 新增，提交中
// deleted / delete / deleted -- 删除，未提交: Original 版本
// changing / commit / deleted -- 删除，提交中: Original 版本
// changed / change / modified -- 修改，未提交
// changing / commit / modified -- 修改，提交中
