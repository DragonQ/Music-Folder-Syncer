﻿#Region " Namespaces "
Imports MusicFolderSyncer.Logger.LogLevel
Imports MusicFolderSyncer.Toolkit
Imports MusicFolderSyncer.SyncSettings
Imports MusicFolderSyncer.Codec.CodecType
Imports System.IO
Imports System.Environment
#End Region

Class FileParser

    Private ProcessID As Int32
    ReadOnly Property FilePath As String
    Private MyGlobalSyncSettings As GlobalSyncSettings
    Private SyncSettings As SyncSettings()

#Region " New "
    Public Sub New(ByRef NewGlobalSyncSettings As GlobalSyncSettings, ByVal NewProcessID As Int32, ByVal NewFilePath As String, Optional NewSyncSettings As SyncSettings = Nothing)
        ProcessID = NewProcessID
        FilePath = NewFilePath
        MyGlobalSyncSettings = NewGlobalSyncSettings
        If NewSyncSettings Is Nothing Then
            SyncSettings = MyGlobalSyncSettings.GetSyncSettings()
        Else
            SyncSettings = {NewSyncSettings}
        End If
    End Sub
#End Region

#Region " Transfer File To Sync Folder "
    Public Function TransferToSyncFolder() As ReturnObject

        Dim MyReturnObject As ReturnObject

        Try
            Dim NewFilesSize As Int64 = 0
            For Each SyncSetting In SyncSettings
                Dim FileCodec As Codec = CheckFileCodec(SyncSetting.GetWatcherCodecs())
                If Not FileCodec Is Nothing Then
                    If CheckFileForSync(FileCodec, SyncSetting) Then
                        Dim SyncFilePath As String = SyncSetting.SyncDirectory & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length)

                        If SyncSetting.TranscodeLosslessFiles AndAlso FileCodec.CompressionType = Lossless Then 'Need to transcode file
                            MyLog.Write(ProcessID, "...transcoding file to " & SyncSetting.Encoder.Name & "...", Debug)
                            TranscodeFile(SyncFilePath, SyncSetting)

                            SyncFilePath = Path.Combine(Path.GetDirectoryName(SyncFilePath), Path.GetFileNameWithoutExtension(SyncFilePath)) &
                                SyncSetting.Encoder.GetFileExtensions(0)
                        Else
                            Directory.CreateDirectory(Path.GetDirectoryName(SyncFilePath))
                            File.Copy(FilePath, SyncFilePath, True)
                        End If

                        Dim NewFile As New FileInfo(SyncFilePath)
                        NewFilesSize += NewFile.Length
                        'Interlocked.Add(SyncFolderSize, NewFile.Length)
                        MyLog.Write(ProcessID, "...successfully added file to sync folder...", Debug)
                    End If
                Else
                    MyLog.Write(ProcessID, "Ignoring file: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """", Information)
                End If
            Next

            MyReturnObject = New ReturnObject(True, "", NewFilesSize)
            MyLog.Write(ProcessID, "File processed: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """", Information)
        Catch ex As Exception
            MyLog.Write(ProcessID, "Processing failed: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """. Exception: " & ex.Message, Warning)
            MyReturnObject = New ReturnObject(False, ex.Message, 0)
        End Try

        Return MyReturnObject

    End Function

    Public Function DeleteInSyncFolder() As ReturnObject

        Dim MyReturnObject As ReturnObject

        Try
            For Each SyncSetting In SyncSettings
                Dim FileCodec As Codec = CheckFileCodec(SyncSetting.GetWatcherCodecs())
                If Not FileCodec Is Nothing Then
                    'File was meant to be synced, which means we now need to delete the synced version
                    Dim SyncFilePath As String = SyncSetting.SyncDirectory & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length)

                    If SyncSetting.TranscodeLosslessFiles AndAlso FileCodec.CompressionType = Lossless Then 'Need to replace extension with .ogg
                        Dim TranscodedFilePath As String = Path.Combine(Path.GetDirectoryName(SyncFilePath), Path.GetFileNameWithoutExtension(SyncFilePath)) &
                                                        SyncSetting.Encoder.GetFileExtensions(0)
                        SyncFilePath = TranscodedFilePath
                    End If

                    'Delete file if it exists in sync folder
                    If File.Exists(SyncFilePath) Then
                        File.Delete(SyncFilePath)
                        MyLog.Write("...file in sync folder deleted: """ & SyncFilePath.Substring(SyncSetting.SyncDirectory.Length) & """.", Information)
                    Else
                        MyLog.Write("...file doesn't exist in sync folder: """ & SyncFilePath.Substring(SyncSetting.SyncDirectory.Length) & """.", Information)
                    End If
                Else
                    Throw New Exception("File was being watched but could not determine its codec.")
                End If
            Next

            MyReturnObject = New ReturnObject(True, "", 0)
            MyLog.Write(ProcessID, "File deleted: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """", Information)
        Catch ex As Exception
            MyLog.Write(ProcessID, "File deletion failed: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """. Exception: " & ex.Message, Warning)
            MyReturnObject = New ReturnObject(False, ex.Message, 0)
        End Try

        Return MyReturnObject

    End Function

    Public Function RenameInSyncFolder(OldFilePath As String) As ReturnObject

        Dim MyReturnObject As ReturnObject

        Try
            For Each SyncSetting In SyncSettings
                Dim FileCodec As Codec = CheckFileCodec(SyncSetting.GetWatcherCodecs())
                If Not FileCodec Is Nothing Then
                    If CheckFileForSync(FileCodec, SyncSetting) Then
                        Dim SyncFilePath As String = SyncSetting.SyncDirectory & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length)
                        Dim OldSyncFilePath As String = SyncSetting.SyncDirectory & OldFilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length)

                        If SyncSetting.TranscodeLosslessFiles AndAlso FileCodec.CompressionType = Lossless Then 'Need to replace extension with .ogg
                            Dim TempString As String = Path.Combine(Path.GetDirectoryName(SyncFilePath), Path.GetFileNameWithoutExtension(SyncFilePath)) &
                                SyncSetting.Encoder.GetFileExtensions(0)
                            SyncFilePath = TempString
                            TempString = Path.Combine(Path.GetDirectoryName(OldSyncFilePath), Path.GetFileNameWithoutExtension(OldSyncFilePath)) &
                                SyncSetting.Encoder.GetFileExtensions(0)
                            OldSyncFilePath = TempString
                        End If

                        If File.Exists(OldSyncFilePath) Then
                            File.Move(OldSyncFilePath, SyncFilePath)
                        Else
                            MyLog.Write("...old file doesn't exist in sync folder: """ & OldSyncFilePath & """, creating now...", Warning)

                            If SyncSetting.TranscodeLosslessFiles AndAlso FileCodec.CompressionType = Lossless Then 'Need to transcode file
                                MyLog.Write("...transcoding file to " & SyncSetting.Encoder.Name & "...", Debug)
                                TranscodeFile(SyncFilePath, SyncSetting)
                            Else
                                Directory.CreateDirectory(Path.GetDirectoryName(SyncFilePath))
                                File.Copy(FilePath, SyncFilePath, True)
                            End If

                            MyLog.Write("...successfully added file to sync folder.", Information)
                        End If

                        MyLog.Write("...successfully renamed file in sync folder.", Information)
                    End If
                Else
                    Throw New Exception("File was being watched but could not determine its codec.")
                End If
            Next

            MyReturnObject = New ReturnObject(True, "", 0)
            MyLog.Write(ProcessID, "File renamed: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """", Information)
        Catch ex As Exception
            MyLog.Write(ProcessID, "File rename failed: """ & FilePath.Substring(MyGlobalSyncSettings.SourceDirectory.Length) & """. Exception: " & ex.Message, Warning)
            MyReturnObject = New ReturnObject(False, ex.Message, 0)
        End Try

        Return MyReturnObject

    End Function

    Private Sub TranscodeFile(FileTo As String, SyncSetting As SyncSettings)

        Dim FileFrom As String = FilePath
        Dim OutputFilePath As String = ""

        Try
            Dim OutputDirectory As String = Path.GetDirectoryName(FileTo)
            OutputFilePath = Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(FileTo)) & SyncSetting.Encoder.GetFileExtensions(0)
            Directory.CreateDirectory(OutputDirectory)
        Catch ex As Exception
            Dim MyError As String = ex.Message
            If ex.InnerException IsNot Nothing Then
                MyError &= NewLine & NewLine & ex.InnerException.ToString
            End If
            MyLog.Write(ProcessID, "...transcode failed [1]. Exception: " & MyError, Warning)
        End Try

        Try
            Dim ffmpeg As New ProcessStartInfo(MyGlobalSyncSettings.ffmpegPath)
            ffmpeg.CreateNoWindow = True
            ffmpeg.UseShellExecute = False

            Dim FiltersString As String = ""
            If SyncSetting.ReplayGain <> ReplayGainMode.None Then
                FiltersString = " -af volume=replaygain=" & SyncSetting.GetReplayGainSetting().ToLower
            End If

            ffmpeg.Arguments = "-i """ & FileFrom & """ -vn -c:a " & SyncSetting.Encoder.GetProfiles(0).Argument & FiltersString & " -hide_banner """ & OutputFilePath & """"
            'libvorbis -aq: 4 = 128 kbps, 5 = 160 kbps, 6 = 192 kbps, 7 = 224 kbps, 8 = 256 kbps

            MyLog.Write(ProcessID, "...ffmpeg arguments: """ & ffmpeg.Arguments & """...", Debug)

            Dim ffmpegProcess As Process = Process.Start(ffmpeg)
            ffmpegProcess.WaitForExit()

            If ffmpegProcess.ExitCode <> 0 Then
                Throw New Exception("ffmpeg exited with an error! (Code: " & ffmpegProcess.ExitCode & ")")
            End If

            MyLog.Write(ProcessID, "...transcode complete...", Debug)
        Catch ex As Exception
            Dim MyError As String = ex.Message
            If ex.InnerException IsNot Nothing Then
                MyError &= NewLine & NewLine & ex.InnerException.ToString
            End If
            MyLog.Write(ProcessID, "...transcode failed [2]. Exception: " & MyError, Warning)
        End Try

    End Sub
#End Region

#Region " File Checks "
    Public Function CheckFileForSync(ByVal FileCodec As Codec, SyncSetting As SyncSettings) As Boolean

        Try
            If CheckFileTags(FileCodec, SyncSetting) Then
                MyLog.Write(ProcessID, "...file has correct tags, now syncing...", Debug)
                Return True
            Else
                MyLog.Write(ProcessID, "...file does not have correct tags, ignoring...", Debug)
                Return False
            End If
        Catch ex As Exception
            MyLog.Write(ProcessID, "...error whilst attempting to parse file. Exception: " & ex.Message, Warning)
            Return False
        End Try

    End Function

    Public Function CheckFileCodec(CodecsToCheck As Codec()) As Codec

        Dim FileExtension As String = Path.GetExtension(FilePath)

        For Each Codec As Codec In CodecsToCheck
            For Each CodecExtension As String In Codec.GetFileExtensions()
                If FileExtension = CodecExtension Then
                    MyLog.Write(ProcessID, "...file type recognised, now checking tags...", Debug)
                    Return Codec
                End If
            Next
        Next

        MyLog.Write(ProcessID, "...file type not recognised, ignoring...", Debug)
        Return Nothing

    End Function

    Private Function CheckFileTags(MyCodec As Codec, SyncSetting As SyncSettings) As Boolean

        Dim TagsObject As ReturnObject = MyCodec.MatchTag(FilePath, SyncSetting.GetWatcherTags)

        If TagsObject.Success Then
            Return CType(TagsObject.MyObject, Boolean)
        Else
            MyLog.Write(ProcessID, "...could not obtain file tags. File: """ & FilePath & """, Codec: " & MyCodec.Name & ", Exception: " & TagsObject.ErrorMessage, Warning)
            Return False
        End If

    End Function
#End Region

End Class