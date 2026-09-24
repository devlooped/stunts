Namespace Global.Stunts
    Partial Friend Class Stunt
        Shared Sub New()
            StuntFactory.[Default] = New DynamicStuntFactory
            OnInitialized()
        End Sub

        ''' <summary>
        ''' Invoked after the default <see cref="StuntFactory.Default"/> 
        ''' is initialized.
        ''' </summary>
        Partial Private Shared Sub OnInitialized()

        End Sub
    End Class
End Namespace