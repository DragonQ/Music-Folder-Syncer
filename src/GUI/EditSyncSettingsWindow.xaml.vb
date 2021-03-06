﻿#Region " Namespaces "
Imports MusicDirectorySyncer.Logger.LogLevel
Imports MusicDirectorySyncer.Toolkit
Imports MusicDirectorySyncer.Toolkit.Browsers
Imports System.IO
#End Region

Public Class EditSyncSettingsWindow

    Private ReadOnly MyGlobalSyncSettings As GlobalSyncSettings

#Region " New "
    Public Sub New(NewGlobalSyncSettings As GlobalSyncSettings)

        ' This call is required by the designer.
        InitializeComponent()
        MyGlobalSyncSettings = NewGlobalSyncSettings

    End Sub

    Private Sub EditSyncSettingsWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        ' Set run-time properties of window objects
        txtSourceDirectory.Text = MyGlobalSyncSettings.SourceDirectory
        txt_ffmpegPath.Text = MyGlobalSyncSettings.ffmpegPath
        spinThreads.Maximum = Environment.ProcessorCount
        spinThreads.Value = MyGlobalSyncSettings.MaxThreads

    End Sub
#End Region

#Region " Window Controls "
    Private Sub btnBrowseSourceDirectory_Click(sender As Object, e As RoutedEventArgs)

        Dim DefaultDirectory As String = txtSourceDirectory.Text

        If Not Directory.Exists(DefaultDirectory) Then
            DefaultDirectory = MyGlobalSyncSettings.SourceDirectory
        End If

        Dim Browser As ReturnObject = CreateDirectoryBrowser(DefaultDirectory, "Select Source Directory")

        If Browser.Success Then
            txtSourceDirectory.Text = CStr(Browser.MyObject)
        End If

    End Sub

    Private Sub btnBrowse_ffmmpegPath_Click(sender As Object, e As RoutedEventArgs)

        Dim DefaultPath As String = txt_ffmpegPath.Text

        If Not Directory.Exists(DefaultPath) Then
            DefaultPath = MyGlobalSyncSettings.ffmpegPath
        End If

        Dim Browser As ReturnObject = CreateFileBrowser_ffmpeg(DefaultPath)

        If Browser.Success Then
            txt_ffmpegPath.Text = CStr(Browser.MyObject)
        End If

    End Sub

    Private Sub btnSave_Click(sender As Object, e As RoutedEventArgs)

        EnableDisableControls(False)

        Try
            ' Check settings aren't nonsense
            If Not Directory.Exists(txtSourceDirectory.Text) Then
                Throw New Exception("Specified source directory doesn't exist.")
            End If

            If Not File.Exists(txt_ffmpegPath.Text) Then
                Throw New Exception("Specified ffmpeg path is not valid.")
            End If

            If Not CInt(spinThreads.Value) > 0 Then
                Throw New Exception("Number of processing threads is not valid.")
            End If

            ' Apply settings
            MyGlobalSyncSettings.SourceDirectory = txtSourceDirectory.Text
            MyGlobalSyncSettings.ffmpegPath = txt_ffmpegPath.Text
            MyGlobalSyncSettings.MaxThreads = CInt(spinThreads.Value)

            ' Save settings to file
            Dim MyResult As ReturnObject = SaveSyncSettings(MyGlobalSyncSettings)

            If MyResult.Success Then
                'Set UserGlobalSyncSettings to our newly updated version now that it's been saved
                UserGlobalSyncSettings = MyGlobalSyncSettings
                MyLog.Write("Syncer settings updated.", Information)
                Me.DialogResult = True
                Me.Close()
            Else
                Throw New Exception(MyResult.ErrorMessage)
            End If
        Catch ex As Exception
            MyLog.Write("Could not update sync settings. Error: " & ex.Message, Warning)
            System.Windows.MessageBox.Show(ex.Message, "Save failed!", MessageBoxButton.OK, MessageBoxImage.Error)
            EnableDisableControls(True)
        End Try

    End Sub

    Private Sub btnCancel_Click(sender As Object, e As RoutedEventArgs)
        EnableDisableControls(False)
        Me.DialogResult = False
        Me.Close()
    End Sub

    Private Sub EnableDisableControls(Enable As Boolean)
        btnSave.IsEnabled = Enable
        btnCancel.IsEnabled = Enable
        txtSourceDirectory.IsEnabled = Enable
        txt_ffmpegPath.IsEnabled = Enable
        spinThreads.IsEnabled = Enable
    End Sub
#End Region

#Region " Window Closing "
    Private Sub MainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing

    End Sub
#End Region

End Class
