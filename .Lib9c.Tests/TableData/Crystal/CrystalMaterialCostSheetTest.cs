using System.Linq;
using Lib9c.TableData.Crystal;
using Xunit;

namespace Lib9c.Tests.TableData.Crystal
{
    public class CrystalMaterialCostSheetTest
    {
        [Fact]
        public void Constructor()
        {
            var tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());

            Assert.NotNull(tableSheets.CrystalMaterialCostSheet);
        }

        [Fact]
        public void Set()
        {
            var sheet = new CrystalMaterialCostSheet();
            sheet.Set(@"item_id,crystal
301000,100");

            Assert.Single(sheet.Values);

            var row = sheet.Values.First();

            Assert.Equal(301000, row.ItemId);
            Assert.Equal(100, row.CRYSTAL);
        }
    }
}
