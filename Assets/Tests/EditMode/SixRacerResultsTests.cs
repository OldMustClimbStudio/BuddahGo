using System;
using System.Linq;
using BuddahGo.Match;
using FishNet.Object;
using NUnit.Framework;
using SteamMultiplayer.Network.Results;
using SteamMultiplayer.UI;
using UnityEngine;
namespace BuddahGo.Tests
{
    public class SixRacerResultsTests
    {
        [Test]
        public void FiveOwnerlessObjectsKeepIndependentIdentities()
        {
            var objects = new GameObject[5];
            try
            {
                for (int i=0;i<5;i++)
                {
                    objects[i]=new GameObject("AI"); objects[i].AddComponent<NetworkObject>();
                    var identity=objects[i].AddComponent<RacerIdentity>();
                    identity.AssignBeforeSpawn(RacerId.ForAI(i),"AI " + i);
                    Assert.That(identity.ClientId,Is.EqualTo(-1));
                    Assert.That(RacerAuthority.TryGetId(identity,out var id),Is.True);
                    Assert.That(id,Is.EqualTo(RacerId.ForAI(i)));
                    for(int j=0;j<5;j++) Assert.That(RacerAuthority.Matches(identity,10000+j),Is.EqualTo(i==j));
                }
            }
            finally {foreach(var obj in objects) if(obj!=null) UnityEngine.Object.DestroyImmediate(obj);}
        }
        [Test]
        public void LegacyFollowCameraCreatesOneMissingSpectatorResolver()
        {
            var obj=new GameObject("camera");
            try
            {
                obj.AddComponent(System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine", true));
                var follower=obj.AddComponent<CinemachineLocalPlayerFollower>();
                var awake=typeof(CinemachineLocalPlayerFollower).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                awake.Invoke(follower,null);awake.Invoke(follower,null);
                Assert.That(obj.GetComponents<RaceSpectatorTargetResolver>().Length,Is.EqualTo(1));
            }
            finally {UnityEngine.Object.DestroyImmediate(obj);}
        }
        [TestCase(-1)] [TestCase(10005)]
        public void WireIdentityRejectsInvalidValues(int id) => Assert.Throws<ArgumentOutOfRangeException>(()=>RacerId.FromValue(id));
        [Test]
        public void SixIndependentClocksAndDnfRowsPreserveActualCompletedLaps()
        {
            var timing=new RaceTiming();timing.Begin(10);
            var rows=new FinalMatchResultEntry[6];
            for(int i=0;i<6;i++)
            {
                var id=i==0?RacerId.FromClient(0):RacerId.ForAI(i-1);
                timing.ObserveCompletedLaps(id,1,100+i);
                if(i<5) {timing.ObserveCompletedLaps(id,2,200+i);timing.Finish(id,3,300+i);}
                Assert.That(timing.TryGetResult(id,out var t),Is.True);
                Assert.That(t.Finished,Is.EqualTo(i<5));
                Assert.That(t.LapSeconds[0],Is.EqualTo(90+i));
                rows[i]=new FinalMatchResultEntry {RacerId=id.Value,FinalRank=i+1,PlayerName="Racer"+i,
                    IsFinished=t.Finished,TotalSeconds=t.Finished?t.TotalSeconds:-1,LapSeconds=t.LapSeconds};
            }
            string text=SoloResultsView.FormatResults(rows);
            Assert.That(text.Split(new[]{"DNF"},StringSplitOptions.None).Length,Is.EqualTo(2));
            Assert.That(text,Does.Contain("6.  Racer5     DNF"));
            Assert.That(text,Does.Contain("01:35.000"));
            Assert.That(text,Does.Contain("04:54.000"));
            Assert.That(text,Does.Not.Contain("00:00.000"));
        }
        [Test]
        public void AllFinishedUsesSixParticipantsAndKeepsFifteenSecondDeadline()
        {
            var policy=new RaceEndPolicy();var rules=new SoloMatchRules(new SoloMatchSettings(5,SoloDifficulty.Normal));
            Assert.That(policy.ShouldEnd(rules,6,1,false,114.9,100,15),Is.False);
            Assert.That(policy.ShouldEnd(rules,6,1,false,115,100,15),Is.True);
            Assert.That(policy.ShouldEnd(rules,6,6,true,101,100,15),Is.True);
        }
    }
}
