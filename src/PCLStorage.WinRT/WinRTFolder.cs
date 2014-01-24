﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace PCLStorage
{
    /// <summary>
    /// Represents a folder in the <see cref="WinRTFileSystem"/>
    /// </summary>
    [DebuggerDisplay("Name = {Name}")]
	public class WinRTFolder : IFolder
	{
        private readonly IStorageFolder _wrappedFolder;
        private readonly bool _isRootFolder;

        /// <summary>
        /// Creates a new <see cref="WinRTFolder"/>
        /// </summary>
        /// <param name="wrappedFolder">The WinRT <see cref="IStorageFolder"/> to wrap</param>
        public WinRTFolder(IStorageFolder wrappedFolder)
        {
            _wrappedFolder = wrappedFolder;
            if (_wrappedFolder.Path == Windows.Storage.ApplicationData.Current.LocalFolder.Path ||
                _wrappedFolder.Path == Windows.Storage.ApplicationData.Current.RoamingFolder.Path)
            {
                _isRootFolder = true;
            }
            else
            {
                _isRootFolder = false;
            }
        }

        /// <summary>
        /// The name of the folder
        /// </summary>
		public string Name
		{
			get { return _wrappedFolder.Name; }
		}

        /// <summary>
        /// The "full path" of the folder, which should uniquely identify it within a given <see cref="IFileSystem"/>
        /// </summary>
		public string Path
		{
			get { return _wrappedFolder.Path; }
		}

        /// <summary>
        /// Creates a file in this folder
        /// </summary>
        /// <param name="desiredName">The name of the file to create</param>
        /// <param name="option">Specifies how to behave if the specified file already exists</param>
        /// <returns>The newly created file</returns>
		public async Task<IFile> CreateFileAsync(string desiredName, CreationCollisionOption option)
		{
            await EnsureExistsAsync().ConfigureAwait(false);
            StorageFile wrtFile;
            try
            {
                wrtFile = await _wrappedFolder.CreateFileAsync(desiredName, GetWinRTCreationCollisionOption(option)).AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024713) // 0x800700B7
                {
                    //  File already exists (and potentially other failures, not sure what the HResult represents)
                    throw new IOException(ex.Message, ex);
                }
                throw;
            }
			return new WinRTFile(wrtFile);
		}

        /// <summary>
        /// Gets a file in this folder
        /// </summary>
        /// <param name="name">The name of the file to get</param>
        /// <returns>The requested file, or null if it does not exist</returns>
		public async Task<IFile> GetFileAsync(string name)
		{
            await EnsureExistsAsync().ConfigureAwait(false);
            var wrtFile = await _wrappedFolder.GetFileAsync(name).AsTask().ConfigureAwait(false);
			return new WinRTFile(wrtFile);
		}

        /// <summary>
        /// Gets a list of the files in this folder
        /// </summary>
        /// <returns>A list of the files in the folder</returns>
		public async Task<IList<IFile>> GetFilesAsync()
		{
            await EnsureExistsAsync().ConfigureAwait(false);
            var wrtFiles = await _wrappedFolder.GetFilesAsync().AsTask().ConfigureAwait(false);
			var files = wrtFiles.Select(f => new WinRTFile(f)).ToList<IFile>();
			return new ReadOnlyCollection<IFile>(files);
		}

        /// <summary>
        /// Creates a subfolder in this folder
        /// </summary>
        /// <param name="desiredName">The name of the folder to create</param>
        /// <param name="option">Specifies how to behave if the specified folder already exists</param>
        /// <returns>The newly created folder</returns>
		public async Task<IFolder> CreateFolderAsync(string desiredName, CreationCollisionOption option)
		{
            await EnsureExistsAsync().ConfigureAwait(false);
			StorageFolder wrtFolder;
            try
            {
                wrtFolder = await _wrappedFolder.CreateFolderAsync(desiredName, GetWinRTCreationCollisionOption(option)).AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024713) // 0x800700B7
                {
                    //  Folder already exists (and potentially other failures, not sure what the HResult represents)
                    throw new IOException(ex.Message, ex);
                }
                throw;
            }
			return new WinRTFolder(wrtFolder);
		}

        /// <summary>
        /// Gets a subfolder in this folder
        /// </summary>
        /// <param name="name">The name of the folder to get</param>
        /// <returns>The requested folder, or null if it does not exist</returns>
		public async Task<IFolder> GetFolderAsync(string name)
		{
            await EnsureExistsAsync().ConfigureAwait(false);
			StorageFolder wrtFolder;
            try
            {
                wrtFolder = await _wrappedFolder.GetFolderAsync(name).AsTask().ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                //  Folder does not exist
                throw new Exceptions.DirectoryNotFoundException(ex.Message, ex);
            }
			return new WinRTFolder(wrtFolder);
		}

        /// <summary>
        /// Gets a list of subfolders in this folder
        /// </summary>
        /// <returns>A list of subfolders in the folder</returns>
		public async Task<IList<IFolder>> GetFoldersAsync()
		{
            await EnsureExistsAsync().ConfigureAwait(false);
            var wrtFolders = await _wrappedFolder.GetFoldersAsync().AsTask().ConfigureAwait(false);
			var folders = wrtFolders.Select(f => new WinRTFolder(f)).ToList<IFolder>();
			return new ReadOnlyCollection<IFolder>(folders);
		}

        /// <summary>
        /// Deletes this folder and all of its contents
        /// </summary>
        /// <returns>A task which will complete after the folder is deleted</returns>
		public async Task DeleteAsync()
		{
            await EnsureExistsAsync().ConfigureAwait(false);

            if (_isRootFolder)
            {
                throw new IOException("Cannot delete root storage folder.");
            }

            await _wrappedFolder.DeleteAsync().AsTask().ConfigureAwait(false);
		}

		Windows.Storage.CreationCollisionOption GetWinRTCreationCollisionOption(CreationCollisionOption option)
		{
			if (option == CreationCollisionOption.GenerateUniqueName)
			{
				return Windows.Storage.CreationCollisionOption.GenerateUniqueName;
			}
			else if (option == CreationCollisionOption.ReplaceExisting)
			{
				return Windows.Storage.CreationCollisionOption.ReplaceExisting;
			}
			else if (option == CreationCollisionOption.FailIfExists)
			{
				return Windows.Storage.CreationCollisionOption.FailIfExists;
			}
			else if (option == CreationCollisionOption.OpenIfExists)
			{
				return Windows.Storage.CreationCollisionOption.OpenIfExists;
			}
			else
			{
				throw new ArgumentException("Unrecognized CreationCollisionOption value: " + option);
			}
		}

        async Task EnsureExistsAsync()
        {
            try
            {
                await StorageFolder.GetFolderFromPathAsync(Path).AsTask().ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                //  Folder does not exist
                throw new Exceptions.DirectoryNotFoundException(ex.Message, ex);
            }
        }
	}
}
