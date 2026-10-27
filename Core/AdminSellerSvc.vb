Imports System.Data
Imports System.Data.OleDb

''' <summary>One seller account with its stall (what the admin Sellers list shows).</summary>
Public Class AdminSellerRow
    Public UserId As Integer
    Public Username As String = ""
    ''' <summary>active | pending | rejected | disabled</summary>
    Public Status As String = "active"
    Public Joined As Date
    Public AvatarPath As String = ""
    Public StallName As String = ""
    Public Category As String = ""
    Public Contact As String = ""
End Class

Public Module AdminSellerSvc
    ''' <summary>Every seller account (all statuses) with its stall, sorted by username.</summary>
    Public Function GetSellers() As List(Of AdminSellerRow)
        Dim dt As DataTable = Db.Query(
            "SELECT u.id, u.username, u.[status], u.created_at, s.[name] AS stall, s.contact, IIf(c.[name] IS NULL, '', c.[name]) AS category " &
            "FROM (users u " &
            "LEFT JOIN stalls s ON s.owner_id = u.id) " &
            "LEFT JOIN stall_categories c ON c.id = s.category_id " &
            "WHERE u.[role] = 'seller' ORDER BY u.username")
        Dim list As New List(Of AdminSellerRow)
        For Each r As DataRow In dt.Rows
            Dim row As New AdminSellerRow()
            row.UserId = Db.GetInt(r, "id")
            row.Username = Db.GetStr(r, "username")
            row.Status = Db.GetStr(r, "status")
            row.Joined = Db.GetDate(r, "created_at")
            row.StallName = Db.GetStr(r, "stall")
            row.Category = Db.GetStr(r, "category")
            row.Contact = Db.GetStr(r, "contact")
            Try
                row.AvatarPath = Avatars.PathOfUser(row.Username)
            Catch
                row.AvatarPath = ""
            End Try
            list.Add(row)
        Next
        Return list
    End Function

    ''' <summary>
    ''' Changes the seller's account status (active, disabled or rejected).
    ''' Active opens the stall; anything else closes it. Customers only see stalls of active sellers anyway.
    ''' </summary>
    Public Function SetStatus(userId As Integer, newStatus As String, ByRef errMsg As String) As Boolean
        If newStatus <> "active" AndAlso newStatus <> "disabled" AndAlso newStatus <> "rejected" Then
            errMsg = "Unknown status."
            Return False
        End If
        Try
            Using cn As OleDbConnection = Db.Open()
                Using tx As OleDbTransaction = cn.BeginTransaction()
                    Try
                        Db.ExecOn(cn, tx, "UPDATE users SET [status]=@s WHERE id=@u AND [role]='seller'", Db.P("@s", newStatus), Db.P("@u", userId))
                        Db.ExecOn(cn, tx, "UPDATE stalls SET is_open=@o WHERE owner_id=@u", Db.P("@o", If(newStatus = "active", 1, 0)), Db.P("@u", userId))
                        tx.Commit()
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
            Return True
        Catch ex As Exception
            errMsg = "Could not change the account: " & ex.Message
            Return False
        End Try
    End Function
End Module
