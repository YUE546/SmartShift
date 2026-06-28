using System;
using System.Linq;
using Xunit;
using SmartShift.Core.Power;

namespace SmartShift.Tests
{
    /// <summary>
    /// PowerPlanManager 测试需要在 Windows 环境下运行
    /// </summary>
    [Trait("Category", "WindowsOnly")]
    public class PowerPlanManagerTests
    {
        [Fact]
        public void GetAllPlans_ReturnsAtLeastTwoPlans()
        {
            var plans = PowerPlanManager.GetAllPlans();
            Assert.True(plans.Count >= 2, "系统应至少有 2 个电源计划");
        }

        [Fact]
        public void EachPlan_HasNonEmptyGuidAndName()
        {
            var plans = PowerPlanManager.GetAllPlans();
            foreach (var plan in plans)
            {
                Assert.NotEqual(Guid.Empty, plan.Id);
                Assert.False(string.IsNullOrEmpty(plan.Name));
            }
        }

        [Fact]
        public void GetActivePlan_ExistsInAllPlans()
        {
            var activePlan = PowerPlanManager.GetActivePlan();
            var allPlans = PowerPlanManager.GetAllPlans();

            bool found = false;
            foreach (var plan in allPlans)
            {
                if (plan.Id == activePlan.Id)
                {
                    found = true;
                    Assert.True(plan.IsActive);
                    break;
                }
            }
            Assert.True(found, "活动计划应在所有计划列表中");
        }

        [Fact]
        public void SetActivePlan_ByGuid_Works()
        {
            var originalPlan = PowerPlanManager.GetActivePlan();

            try
            {
                // 切换到 Balanced
                bool result = PowerPlanManager.SetActivePlan(PowerPlanManager.Balanced);
                Assert.True(result);

                var currentPlan = PowerPlanManager.GetActivePlan();
                Assert.Equal(PowerPlanManager.Balanced, currentPlan.Id);
            }
            finally
            {
                // 恢复原计划
                PowerPlanManager.SetActivePlan(originalPlan.Id);
            }
        }

        [Fact]
        public void SetActivePlan_InvalidGuid_ReturnsFalse()
        {
            bool result = PowerPlanManager.SetActivePlan(Guid.NewGuid());
            Assert.False(result);
        }

        [Fact]
        public void SetActivePlanByName_Balanced_Works()
        {
            var originalPlan = PowerPlanManager.GetActivePlan();

            try
            {
                // 通过 GUID 查找 Balanced 计划的实际名称（系统可能为非英文）
                var plans = PowerPlanManager.GetAllPlans();
                var balancedPlan = plans.FirstOrDefault(p => p.Id == PowerPlanManager.Balanced);
                Assert.NotNull(balancedPlan);

                bool result = PowerPlanManager.SetActivePlanByName(balancedPlan.Name);
                Assert.True(result);
            }
            finally
            {
                PowerPlanManager.SetActivePlan(originalPlan.Id);
            }
        }
    }
}
