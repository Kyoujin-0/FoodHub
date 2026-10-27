Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Colors, fonts and small drawing helpers shared by the whole app.</summary>
Public Module Theme
    Public Const AppName As String = "FoodHub"
    Public Const Tagline As String = "Every food stall, one order."

    Public ReadOnly Primary As Color = ColorTranslator.FromHtml("#FF6B35")
    Public ReadOnly PrimaryDark As Color = ColorTranslator.FromHtml("#E5561F")
    Public ReadOnly PrimarySoft As Color = ColorTranslator.FromHtml("#FFEFE6")
    Public ReadOnly Bg As Color = ColorTranslator.FromHtml("#FAF7F2")
    Public ReadOnly Surface As Color = Color.White
    Public ReadOnly FieldBg As Color = ColorTranslator.FromHtml("#F6F2EC")
    Public ReadOnly TextDark As Color = ColorTranslator.FromHtml("#2A2623")
    Public ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#8C857D")
    Public ReadOnly Border As Color = ColorTranslator.FromHtml("#E9E2D9")
    Public ReadOnly Success As Color = ColorTranslator.FromHtml("#23A055")
    Public ReadOnly SuccessSoft As Color = ColorTranslator.FromHtml("#E3F6EA")
    Public ReadOnly Danger As Color = ColorTranslator.FromHtml("#D64545")
    Public ReadOnly DangerSoft As Color = ColorTranslator.FromHtml("#FCE8E8")
    Public ReadOnly Warning As Color = ColorTranslator.FromHtml("#E39A12")
    Public ReadOnly WarningSoft As Color = ColorTranslator.FromHtml("#FFF3D6")
    Public ReadOnly Info As Color = ColorTranslator.FromHtml("#3B7BDB")
    Public ReadOnly InfoSoft As Color = ColorTranslator.FromHtml("#E6EFFC")
    Public ReadOnly Star As Color = ColorTranslator.FromHtml("#F5B301")

    Private ReadOnly _fonts As New Dictionary(Of String, Font)

    ''' <summary>Cached Segoe UI font.</summary>
    Public Function F(size As Single, Optional bold As Boolean = False) As Font
        Dim key As String = size.ToString() & "|" & bold.ToString()
        If Not _fonts.ContainsKey(key) Then
            Dim st As FontStyle = FontStyle.Regular
            If bold Then st = FontStyle.Bold
            _fonts(key) = New Font("Segoe UI", size, st, GraphicsUnit.Point)
        End If
        Return _fonts(key)
    End Function

    Public Function RoundRectPath(r As Rectangle, radius As Integer) As GraphicsPath
        Dim p As New GraphicsPath()
        If radius <= 0 OrElse r.Width < 2 OrElse r.Height < 2 Then
            p.AddRectangle(r)
            Return p
        End If
        Dim d As Integer = Math.Min(radius * 2, Math.Min(r.Width, r.Height))
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function

    ''' <summary>Rounded only at the top two corners (used for card cover images).</summary>
    Public Function RoundTopPath(r As Rectangle, radius As Integer) As GraphicsPath
        Dim p As New GraphicsPath()
        If radius <= 0 OrElse r.Width < 2 OrElse r.Height < 2 Then
            p.AddRectangle(r)
            Return p
        End If
        Dim d As Integer = Math.Min(radius * 2, Math.Min(r.Width, r.Height))
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddLine(r.Right, r.Bottom, r.X, r.Bottom)
        p.CloseFigure()
        Return p
    End Function

    Public Function Peso(amount As Decimal) As String
        Return ChrW(&H20B1) & amount.ToString("N2")
    End Function

    ''' <summary>Two-colour gradient pair picked from a name, so placeholders look different per item.</summary>
    Public Function PlaceholderColors(seed As String) As Color()
        Dim pairs As String() = {"#FF8A5B,#FF5E62", "#FFB347,#FF7E5F", "#43CEA2,#185A9D", "#F7971E,#FFD200", "#C471ED,#F64F59", "#12C2E9,#C471ED", "#56AB2F,#A8E063", "#EF629F,#EECDA3"}
        Dim h As Long = 0
        If seed IsNot Nothing Then
            For Each ch As Char In seed
                h = (h * 31 + AscW(ch)) Mod 1000003L
            Next
        End If
        Dim parts As String() = pairs(CInt(h Mod pairs.Length)).Split(","c)
        Return New Color() {ColorTranslator.FromHtml(parts(0)), ColorTranslator.FromHtml(parts(1))}
    End Function

    Public Function Initials(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "?"
        Dim words As String() = name.Trim().Split(" "c)
        If words.Length = 1 Then Return words(0).Substring(0, 1).ToUpper()
        Return (words(0).Substring(0, 1) & words(1).Substring(0, 1)).ToUpper()
    End Function
End Module
