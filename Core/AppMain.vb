Imports System.Windows.Forms

Public Module Program
    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.AddMessageFilter(New WheelFilter())

        Dim errMsg As String = ""
        ' creates database\foodhub.accdb and its tables on the very first run
        If Not AccessSchema.EnsureDatabase(errMsg) OrElse Not Db.TestConnection(errMsg) Then
            MessageBox.Show("Could not open the Microsoft Access database." & vbCrLf & vbCrLf &
                            "File: " & Db.DbFile() & vbCrLf & vbCrLf &
                            "1) Install the Microsoft Access Database Engine (it comes with Microsoft Access / Office)." & vbCrLf &
                            "    Its 32/64-bit version must match this app (the app is 64-bit unless you changed PlatformTarget in FoodHub.vbproj)." & vbCrLf &
                            "2) Make sure foodhub.accdb is not open in Access and its folder is not read-only." & vbCrLf &
                            "3) The path can be changed in db.config." & vbCrLf & vbCrLf & errMsg,
                            Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        Try
            Seeder.EnsureSeeded()
        Catch ex As Exception
            MessageBox.Show("Database error while preparing sample data." & vbCrLf & vbCrLf & ex.Message,
                            Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        Do
            Dim auth As New AuthForm()
            Dim res As DialogResult = auth.ShowDialog()
            auth.Dispose()
            If res <> DialogResult.OK Then Exit Do

            Dim again As Boolean = False
            If Session.Role = "seller" Then
                If SellerSvc.GetMyStall() Is Nothing Then
                    Ui.Warn("This seller account has no stall.")
                    again = True
                Else
                    Dim sellerShell As New SellerForm()
                    sellerShell.ShowDialog()
                    again = sellerShell.LoggedOut
                    sellerShell.Dispose()
                End If
            ElseIf Session.Role = "admin" Then
                Dim adminShell As New AdminForm()
                adminShell.ShowDialog()
                again = adminShell.LoggedOut
                adminShell.Dispose()
            Else
                Dim shell As New CustomerForm()
                shell.ShowDialog()
                again = shell.LoggedOut
                shell.Dispose()
            End If
            Session.Clear()
            If Not again Then Exit Do
        Loop
    End Sub
End Module
