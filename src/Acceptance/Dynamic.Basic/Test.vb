Imports System
Imports Stunts
Imports Xunit

Namespace Sample

    Public Class Tests

        <Fact>
        Public Sub CanConfigureDefaultValues()
            Dim reference = Stunt.[For](Of ICalculator, IDisposable)()
            Dim calculator As ICalculator = reference.ToObject()

            Assert.IsNotType(Of CompiledStuntFactory)(StuntFactory.[Default])

            Dim recorder = New RecordingBehavior()
            reference.AddBehavior(recorder)
            reference.AddBehavior(New DefaultValueBehavior())

            Assert.IsAssignableFrom(Of IDisposable)(calculator)

            Assert.Equal(0, calculator.Add(5, 10))
            Assert.Single(recorder.Invocations)

            Console.WriteLine(recorder.ToString())
        End Sub

    End Class

End Namespace