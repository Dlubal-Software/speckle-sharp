﻿using System.Diagnostics;
using NUnit.Framework;
using System;
using Speckle.Models;

namespace Tests
{
  [TestFixture]
  public class Hashing
  {
    
    [Test(Description = "Checks that hashing (as represented by object ids) actually works.")]
    public void HashChangeCheck()
    {
      var table = new DiningTable();
      var secondTable = new DiningTable();

      Assert.AreEqual(table.GetId(), secondTable.GetId());

      ((dynamic)secondTable).testProp = "wonderful";

      Assert.AreNotEqual(table.GetId(), secondTable.GetId());
    }

    [Test(Description = "Tests the convention that dynamic properties that have key names prepended with '__' are ignored.")]
    public void IgnoredDynamicPropertiesCheck()
    {
      var table = new DiningTable();
      var originalHash = table.GetId();

      ((dynamic)table).__testProp = "wonderful";

      Assert.AreEqual(originalHash, table.GetId());
    }

    [Test(Description = "Rather stupid test as results vary wildly even on one machine.")]
    public void HashingPerformance()
    {
      var polyline = new Polyline();

      for (int i = 0; i < 10000; i++)
        polyline.Points.Add(new Point() { X = i * 2, Y = i % 2 });

      var stopWatch = new Stopwatch();
      stopWatch.Start();

      // Warm-up: first hashing always takes longer due to json serialisation init
      var h1 = polyline.GetId();
      var stopWatchStep = stopWatch.ElapsedMilliseconds;

      stopWatchStep = stopWatch.ElapsedMilliseconds;
      var h2 = polyline.GetId();

      var diff1 = stopWatch.ElapsedMilliseconds - stopWatchStep;
      Assert.True(diff1 < 300, $"Hashing shouldn't take that long ({diff1} ms) for the test object used.");
      Console.WriteLine($"Big obj hash duration: {diff1} ms");

      var pt = new Point() { X = 10, Y = 12, Z = 30 };
      stopWatchStep = stopWatch.ElapsedMilliseconds;
      var h3 = pt.GetId();

      var diff2 = stopWatch.ElapsedMilliseconds - stopWatchStep;
      Assert.True(diff2 < 10, $"Hashing shouldn't take that long  ({diff2} ms)for the point object used.");
      Console.WriteLine($"Small obj hash duration: {diff2} ms");
    }

    [Test(Description ="The hash of a decomposed object is different that that of a non-decomposed object.")]
    public void DecompositionHashes()
    {
      var table = new DiningTable();
      ((dynamic)table)["@decomposeMePlease"] = new Point();

      var hash1 = table.GetId();
      var hash2 = table.GetId(true);

      Assert.AreNotEqual(hash1, hash2);
    }

    [Test(Description = "Checks to see if abstract object wrappers actually work.")]
    public void AbstractHashing()
    {
      var nk1 = new NonKitClass();
      var abs1 = new Abstract(nk1);

      var nk2 = new NonKitClass() { TestProp = "HEllo" };
      var abs2 = new Abstract(nk2);

      var abs1H = abs1.GetId();
      var abs2H = abs2.GetId();

      Assert.AreNotEqual(abs1H, abs2H);

      nk1.TestProp = "Wow";

      Assert.AreNotEqual(abs1H, abs1.GetId());
    }
  }
}
