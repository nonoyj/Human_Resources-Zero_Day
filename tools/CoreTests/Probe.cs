using System;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game.Interaction;
class P {
  static void Main() {
    var a = new ApplicantRecord();
    a.archetypeRaw = (int)Archetype.SyntheticPolished;
    a.groundTruthRaw = (int)GroundTruth.Synthetic;
    a.tellsPresent.Add(TellCatalog.PortraitBlendSeam);
    var l = new EvidenceLedger();
    l.Begin(a);
    var r1 = l.InspectArtifact("seam", 1);
    Console.WriteLine($"first: proven={r1.Proven} tell={r1.TellId} changed={r1.Changed} evidence={r1.IsEvidence} moreTime={r1.RequiresMoreTime}");
    Console.WriteLine($"map seam -> tell {EvidenceMap.ForArtifact("seam")?.TellId} style {EvidenceMap.ForArtifact("seam")?.Style} surface {EvidenceMap.ForArtifact("seam")?.Surface}");
    Console.WriteLine($"catalog seam present = {TellCatalog.PortraitBlendSeam}");
    Console.WriteLine($"unlocked day1 = {TellCatalog.IsUnlocked(1, EvidenceMap.ForArtifact("seam").Surface)}");
  }
}
