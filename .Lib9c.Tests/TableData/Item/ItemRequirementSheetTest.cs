﻿using Lib9c.TableData.Item;
using Xunit;

namespace Lib9c.Tests.TableData.Item
{
    public class ItemRequirementSheetTest
    {
        private const string _csv =
            @"item_id,level,mimis_level
10100000,1,1";

        [Theory]
        [InlineData(_csv)]
        public void Set(string csv)
        {
            var sheet = new ItemRequirementSheet();
            sheet.Set(csv);
            Assert.Single(sheet);
            Assert.NotNull(sheet.First);

            var row = sheet.First;
            Assert.Equal(row.ItemId, row.Key);
            Assert.Equal(10100000, row.ItemId);
            Assert.Equal(1, row.Level);
        }
    }
}
