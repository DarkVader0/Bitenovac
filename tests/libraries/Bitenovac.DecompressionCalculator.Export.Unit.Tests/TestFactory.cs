using System.Text;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

/// <summary>
/// Provides plans and their calculated dives for the export tests.
/// </summary>
internal static class TestFactory
{
    public static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    public static DivePlanDocument Plan(UnitSystem units = UnitSystem.Metric) =>
        DivePlanDocument.CreateNew(new Guid("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7"), "Reef", units, Now);

    public static DivePlanDocument DecompressionPlan()
    {
        var plan = Plan();
        var dive = plan.Dives[0] with
        {
            Levels = [new LevelInput(40, 25)],
            Cylinders =
            [
                new CylinderInput(21, 0, 24, 0, 230, CylinderPurpose.BottomGas),
                new CylinderInput(50, 0, 11, 0, 200, CylinderPurpose.DecoGas)
            ]
        };
        return plan with { Dives = [dive] };
    }

    public static DivePlanDocument SeriesPlan()
    {
        var plan = DecompressionPlan();
        return plan with { Dives = [plan.Dives[0], plan.Dives[0] with { SurfaceIntervalMinutes = 90, Levels = [new LevelInput(20, 30)] }] };
    }

    public static DivePlanDocument FailingPlan()
    {
        var plan = Plan();
        return plan with
        {
            Dives = [plan.Dives[0] with { Cylinders = [new CylinderInput(100, 0, 7, 0, 200, CylinderPurpose.DecoGas)] }]
        };
    }

    public static DivePlanDocument ViolatingPlan()
    {
        var plan = Plan();
        return plan with
        {
            Dives =
            [
                plan.Dives[0] with
                {
                    Levels = [new LevelInput(40, 30), new LevelInput(3, 5)],
                    Cylinders = [new CylinderInput(21, 0, 24, 0, 230, CylinderPurpose.BottomGas)]
                }
            ]
        };
    }

    public static DivePlanDocument InvalidPlan()
    {
        var plan = Plan();
        return plan with { Dives = [plan.Dives[0] with { Levels = [new LevelInput(0, 20)] }] };
    }

    public static DivePlanDocument ClosedCircuitPlan()
    {
        var plan = Plan(UnitSystem.Imperial);
        return plan with
        {
            Dives =
            [
                plan.Dives[0] with
                {
                    Circuit = new ClosedCircuitInput(1.2, 1.3),
                    Levels = [new LevelInput(130, 25)],
                    Cylinders =
                    [
                        new CylinderInput(21, 35, 19, 3000, 3000, CylinderPurpose.Diluent),
                        new CylinderInput(100, 0, 19, 3000, 3000, CylinderPurpose.Oxygen)
                    ]
                }
            ]
        };
    }

    public static DiveResult Calculate(DivePlanDocument plan) => PlanCalculator.Calculate(plan).Dives[0];

    /// <summary>
    /// Returns a PDF file as text, one character per byte, so its structure and uncompressed content can be searched.
    /// </summary>
    public static string Read(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    /// <summary>
    /// Returns a text as it appears in a PDF content stream, inside a string literal shown with Tj.
    /// </summary>
    public static string Shown(string text) => $"{Encoding.Latin1.GetString(Pdf.PdfFontMetrics.EncodeLiteral(text))} Tj";
}
