﻿Imports TagLib


Public Class Codec

    Property Name As String
    Property Type As CodecType
    Property Profiles As Profile()
    Property FileExtensions As String()

    Enum CodecType
        Lossless
        Lossy
    End Enum


    Public Sub New(MyName As String, MyType As String, MyProfiles As Profile(), Extensions As String())

        Name = MyName
        Profiles = MyProfiles
        FileExtensions = Extensions
        Type = SetType(MyType)

    End Sub

    Public Sub New(MyCodec As Codec, MyProfile As Profile)

        Name = MyCodec.Name
        Profiles = {MyProfile}
        FileExtensions = MyCodec.FileExtensions
        Type = SetType(MyCodec.GetTypeString)

    End Sub

    Private Function SetType(TypeString As String) As CodecType

        Select Case TypeString
            Case Is = "Lossless"
                Return CodecType.Lossless
            Case Is = "Lossy"
                Return CodecType.Lossy
            Case Else
                Throw New Exception("Codec type """ & TypeString & """ not recognised.")
        End Select

    End Function

    Public Function GetTypeString() As String

        Select Case Type
            Case Is = CodecType.Lossless
                Return "Lossless"
            Case Is = CodecType.Lossy
                Return "Lossy"
            Case Else
                Return Nothing
        End Select

    End Function

    Public Overridable Function MatchTag(FilePath As String, Tags As Tag()) As ReturnObject

        Select Case Me.Name
            Case Is = "FLAC", "OGG Vorbis"
                Return FlacCodec.MatchTag(FilePath, Tags)
            Case Is = "WMA Lossless", "WMA"
                Return WMACodec.MatchTag(FilePath, Tags)
            Case Is = "MP3"
                Return MP3Codec.MatchTag(FilePath, Tags)
            Case Is = "AAC"
                Return AACCodec.MatchTag(FilePath, Tags)
            Case Else
                Return New ReturnObject(False, "Codec not recognised: " & Me.Name, Nothing)
        End Select

    End Function

    Public Class Profile
        Property Name As String
        'Property Type As ProfileType
        Property Argument As String

        'Enum ProfileType
        '    CBR
        '    VBR
        'End Enum

        Public Sub New(MyName As String, MyArgument As String)

            Name = MyName
            'Type = MyType
            Argument = MyArgument

        End Sub
    End Class

    Public Class Tag
        Property Name As String
        Property Value As String

        Public Sub New(MyName As String, Optional MyValue As String = Nothing)

            Name = MyName
            Value = MyValue

        End Sub
    End Class

    Class Mpeg4TestFile
        Inherits Mpeg4.File
        Public Sub New(path As String)

            MyBase.New(path)
        End Sub

        Public Shadows ReadOnly Property UdtaBoxes() As List(Of Mpeg4.IsoUserDataBox)
            Get
                Return MyBase.UdtaBoxes
            End Get
        End Property
    End Class

    Class FlacCodec

        Public Shared Function MatchTag(FilePath As String, Tags As Tag()) As ReturnObject

            Try
                Dim FlacFile As New Flac.File(FilePath)
                Dim Xiph As Ogg.XiphComment = CType(FlacFile.GetTag(TagTypes.Xiph, False), Ogg.XiphComment)

                If Xiph Is Nothing Then
                    Throw New Exception("FLAC tags not found.")
                Else
                    'Search for each requested tag
                    For Each MyTag As Tag In Tags
                        Dim Results As String() = Xiph.GetField(MyTag.Name)

                        If Not Results Is Nothing AndAlso Results.Length > 0 Then 'Tag we're looking for is present, so continue
                            'Value matches or wasn't requested, so return true
                            If MyTag.Value Is Nothing OrElse MyTag.Value.ToUpper = Results(0).Trim.ToUpper Then
                                Return New ReturnObject(True, "", True)
                            End If
                        End If
                    Next

                    'If none of the tags were found, return false
                    Return New ReturnObject(True, "", False)
                End If
            Catch ex As Exception
                Return New ReturnObject(False, ex.Message, Nothing)
            End Try

        End Function

    End Class

    Class WMACodec

        Public Shared Function MatchTag(FilePath As String, Tags As Tag()) As ReturnObject

            Try
                Dim WMAFile As TagLib.File = TagLib.File.Create(FilePath)
                Dim ASF As Asf.Tag = CType(WMAFile.GetTag(TagTypes.Asf, False), Asf.Tag)

                If ASF Is Nothing Then
                    Throw New Exception("WMA tags not found.")
                Else
                    'Search for each requested tag
                    For Each MyTag As Tag In Tags
                        Dim MatchFound As Asf.ContentDescriptor = Nothing

                        For Each Field As Asf.ContentDescriptor In ASF
                            Dim FieldName As String = Field.Name.Trim.ToUpper
                            If FieldName = MyTag.Name.ToUpper Then
                                MatchFound = Field
                                Exit For
                            ElseIf FieldName.Contains(MyTag.Name.ToUpper) Then 'Could be a match, need to do an extra check...
                                Dim TagSplit As String() = FieldName.Split("/"c)

                                If TagSplit.Count > 1 AndAlso TagSplit(1).Trim.ToUpper = MyTag.Name.ToUpper Then
                                    MatchFound = Field
                                    Exit For
                                End If
                            End If
                        Next

                        If Not MatchFound Is Nothing Then 'If the value matches or wasn't requested, return true
                            If MyTag.Value Is Nothing OrElse MyTag.Value.ToUpper = ASF.GetDescriptorString(MatchFound.Name).Trim.ToUpper Then
                                Return New ReturnObject(True, "", True)
                            End If
                        End If
                    Next

                    'If none of the tags was found, return false
                    Return New ReturnObject(True, "", False)
                End If

                Return New ReturnObject(True, "", False)
            Catch ex As Exception
                Return New ReturnObject(False, ex.Message, Nothing)
            End Try

        End Function

    End Class

    Class MP3Codec

        Public Shared Function MatchTag(FilePath As String, Tags As Tag()) As ReturnObject

            Try
                Dim MP3File As TagLib.File = TagLib.File.Create(FilePath)
                Dim ID3 As Id3v2.Tag = CType(MP3File.GetTag(TagTypes.Id3v2, False), Id3v2.Tag)

                If ID3 Is Nothing Then
                    Throw New Exception("MP3 tags not found.")
                Else
                    Dim ID3Frames As List(Of Id3v2.Frame) = ID3.GetFrames.ToList
                    Dim ID3UserFrame As Id3v2.UserTextInformationFrame

                    'Search for each requested tag
                    For Each ID3Frame As Id3v2.Frame In ID3Frames
                        Try 'Test if this is a user-defined ID3v2 tag - if not, skip to the next one
                            ID3UserFrame = TryCast(ID3Frame, Id3v2.UserTextInformationFrame)

                            If Not ID3UserFrame Is Nothing Then
                                For Each MyTag As Tag In Tags
                                    If ID3UserFrame.Description.Trim.ToUpper = MyTag.Name.ToUpper Then
                                        'If the value matches or wasn't requested, return true
                                        If MyTag.Value Is Nothing OrElse MyTag.Value.ToUpper = ID3UserFrame.Text(0).Trim.ToUpper Then
                                            Return New ReturnObject(True, "", True)
                                        End If
                                    End If
                                Next
                            End If
                        Catch ex As Exception
                            Continue For
                        End Try
                    Next

                    'If none of the tags was found, return false
                    Return New ReturnObject(True, "", False)
                End If

                Return New ReturnObject(True, "", False)
            Catch ex As Exception
                Return New ReturnObject(False, ex.Message, Nothing)
            End Try

        End Function

    End Class

    Class AACCodec

        Public Shared Function MatchTag(FilePath As String, Tags As Tag()) As ReturnObject

            Dim BOXTYPE_ILST As ReadOnlyByteVector = "ilst" 'List of tags
            Dim BOXTYPE_NAME As ReadOnlyByteVector = "name" 'Tag name
            Dim BOXTYPE_DATA As ReadOnlyByteVector = "data" 'Tag value

            Try
                'Grab user metadata box, which contains all of our tags
                Dim AAC_File As New Mpeg4TestFile(FilePath)
                Dim UserDataBoxes As Mpeg4.IsoUserDataBox = AAC_File.UdtaBoxes(0)
                Dim UserDataBox = DirectCast(UserDataBoxes.Children.First(), Mpeg4.IsoMetaBox)

                Dim TagMatched As Boolean = False

                'Search through each box until we find the "ilst" box
                For a As Int32 = 0 To UserDataBox.Children.Count - 1
                    Try
                        If UserDataBox.Children(a).BoxType = BOXTYPE_ILST Then
                            'Search through child boxes of "ilst" box to find the relevant tags
                            For Each UserData As Mpeg4.AppleAnnotationBox In CType(UserDataBox.Children(a), Mpeg4.AppleItemListBox).Children
                                Dim TagFound As Tag = Nothing

                                'If this AnnotationBox has children, look through them for tag data
                                If UserData.Children.Count > 0 Then
                                    For Each TagBox In UserData.Children
                                        If TagBox.BoxType = BOXTYPE_NAME Then

                                            Debug.WriteLine(CType(TagBox, Mpeg4.AppleAdditionalInfoBox).Text.Replace(Convert.ToChar(0), "").Trim)

                                            'This AppleAdditionalInfoBox contains the name of the tag, so look for it in our list of tag names
                                            For Each MyTag As Tag In Tags
                                                If CType(TagBox, Mpeg4.AppleAdditionalInfoBox).Text.Replace(Convert.ToChar(0), "").Trim.ToUpper = MyTag.Name.ToUpper Then
                                                    TagFound = MyTag
                                                    Exit For
                                                End If
                                            Next
                                        ElseIf TagBox.BoxType = BOXTYPE_DATA Then
                                            'This AppleAdditionalInfoBox contains the value of the tag, so if this tag was found in our tag list
                                            'we need to check if the tag's value also matches (or that no specific value was requested)
                                            If Not TagFound Is Nothing Then
                                                If TagFound.Value Is Nothing OrElse CType(TagBox, Mpeg4.AppleDataBox).Text.Trim.ToUpper = TagFound.Value.ToUpper Then
                                                    TagMatched = True
                                                    Exit For
                                                End If
                                            End If
                                        End If
                                    Next
                                End If

                                'If we matched a tag, we can end our search now
                                If TagMatched Then Exit For
                            Next
                        End If
                    Catch ex As Exception
                        Return New ReturnObject(False, ex.Message, Nothing)
                    End Try

                    'If we matched a tag, we can end our search now
                    If TagMatched Then Exit For
                Next

                If TagMatched Then
                    Return New ReturnObject(True, "", True)
                Else
                    Return New ReturnObject(True, "", False)
                End If
            Catch ex As Exception
                Return New ReturnObject(False, ex.Message, Nothing)
            End Try

        End Function

    End Class

End Class
