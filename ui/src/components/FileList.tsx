import React, { useRef, useState } from 'react';

interface FileItem {
  name: string;
  type: 'file' | 'folder';
  size: number;
  modifiedDate: string;
}

interface FileListResponse {
  items: FileItem[];
  currentFolder: string;
  parentFolder: string | null;
}

interface FileListProps {
  fileData: FileListResponse;
  apiBase: string;
  currentFolder: string;
  searchTerm?: string;
  onDelete: () => void;
  onNavigate: (folderName: string) => void;
  onNavigateUp: () => void;
  onUpload: () => void;
  onCreateFolder: (name: string) => void;
}

const FileList: React.FC<FileListProps> = ({ 
  fileData, 
  apiBase, 
  currentFolder, 
  searchTerm,
  onDelete, 
  onNavigate, 
  onNavigateUp,
  onUpload,
  onCreateFolder
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [showCreateFolderModal, setShowCreateFolderModal] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [uploading, setUploading] = useState(false);
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [error, setError] = useState('');

  const handleFileUploadClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileSelected = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const maxFileSize = 100 * 1024 * 1024; // 100MB
    if (file.size === 0) {
      setError('File is empty.');
      return;
    }
    if (file.size > maxFileSize) {
      setError(`File '${file.name}' exceeds the 100MB size limit.`);
      return;
    }

    setUploading(true);
    setError('');
    const formData = new FormData();
    formData.append('file', file);
    if (currentFolder) {
      formData.append('folderPath', currentFolder);
    }

    try {
      const res = await fetch(`${apiBase}/files/upload`, {
        method: 'POST',
        body: formData,
      });
      
      if (!res.ok) {
        let message = 'Upload failed';
        try {
          const text = await res.text();
          if (text) {
            try {
              const json = JSON.parse(text);
              message = json.message || json.error || text;
            } catch {
              message = text;
            }
          }
        } catch {}
        throw new Error(message);
      }
      
      onUpload();
      if (fileInputRef.current) fileInputRef.current.value = '';
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setUploading(false);
    }
  };

  const handleCreateFolderClick = () => {
    setShowCreateFolderModal(true);
    setNewFolderName('');
    setError('');
  };

  const handleCreateFolderConfirm = async () => {
    if (!newFolderName.trim()) return;
    
    setCreatingFolder(true);
    setError('');
    try {
      await onCreateFolder(newFolderName.trim());
      setShowCreateFolderModal(false);
      setNewFolderName('');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setCreatingFolder(false);
    }
  };

  const handleCreateFolderCancel = () => {
    setShowCreateFolderModal(false);
    setNewFolderName('');
    setError('');
  };
  const handleDownload = (filename: string) => {
    const params = new URLSearchParams();
    
    if (searchTerm) {
      // When searching, filename contains the full relative path
      const pathParts = filename.split('/');
      const actualFilename = pathParts[pathParts.length - 1];
      const folderPath = pathParts.length > 1 ? pathParts.slice(0, -1).join('/') : '';
      
      params.set('filename', actualFilename);
      if (folderPath) {
        params.set('folder', folderPath);
      }
    } else {
      // Normal browsing mode
      params.set('filename', filename);
      if (currentFolder) {
        params.set('folder', currentFolder);
      }
    }
    
    window.open(`${apiBase}/files/download?${params.toString()}`);
  };

  const handleDeleteFile = async (filename: string) => {
    if (!window.confirm(`Delete ${filename}?`)) return;
    
    const params = new URLSearchParams();
    
    if (searchTerm) {
      // When searching, filename contains the full relative path
      const pathParts = filename.split('/');
      const actualFilename = pathParts[pathParts.length - 1];
      const folderPath = pathParts.length > 1 ? pathParts.slice(0, -1).join('/') : '';
      
      params.set('filename', actualFilename);
      if (folderPath) {
        params.set('folder', folderPath);
      }
    } else {
      // Normal browsing mode
      params.set('filename', filename);
      if (currentFolder) {
        params.set('folder', currentFolder);
      }
    }
    
    const res = await fetch(`${apiBase}/files/file?${params.toString()}`, {
      method: 'DELETE',
    });
    if (res.ok) onDelete();
  };

  const handleDeleteFolder = async (folderName: string) => {
    if (!window.confirm(`Delete folder "${folderName}" and all its contents?`)) return;
    
    const params = new URLSearchParams();
    
    if (searchTerm) {
      // When searching, folderName contains the full relative path
      params.set('folder', folderName);
    } else {
      // Normal browsing mode
      const folderPath = currentFolder ? `${currentFolder}/${folderName}` : folderName;
      params.set('folder', folderPath);
    }
    
    const res = await fetch(`${apiBase}/files/folder?${params.toString()}`, {
      method: 'DELETE',
    });
    if (res.ok) onDelete();
  };

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString() + ' ' + new Date(dateString).toLocaleTimeString();
  };

  return (
    <div>
      {/* Action buttons */}
      <div style={{ 
        display: 'flex', 
        gap: '1rem', 
        marginBottom: '1rem', 
        padding: '1rem',
        backgroundColor: '#f9fafb',
        borderRadius: '8px',
        border: '1px solid #e5e7eb'
      }}>
        <button
          onClick={handleFileUploadClick}
          disabled={uploading}
          style={{
            padding: '0.75rem 1.5rem',
            backgroundColor: uploading ? '#e5e7eb' : '#2563eb',
            color: uploading ? '#6b7280' : 'white',
            border: 'none',
            borderRadius: '6px',
            cursor: uploading ? 'not-allowed' : 'pointer',
            fontSize: '14px',
            fontWeight: '600',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem'
          }}
        >
          📁 {uploading ? 'Uploading...' : 'Upload File'}
        </button>
        
        <button
          onClick={handleCreateFolderClick}
          style={{
            padding: '0.75rem 1.5rem',
            backgroundColor: '#10b981',
            color: 'white',
            border: 'none',
            borderRadius: '6px',
            cursor: 'pointer',
            fontSize: '14px',
            fontWeight: '600',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem'
          }}
        >
          📂 Create Folder
        </button>
      </div>

      {/* Hidden file input */}
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileSelected}
        style={{ display: 'none' }}
      />

      {/* Error message */}
      {error && (
        <div style={{
          color: '#dc2626',
          backgroundColor: '#fef2f2',
          border: '1px solid #fca5a5',
          borderRadius: '6px',
          padding: '0.75rem',
          marginBottom: '1rem',
          fontSize: '14px'
        }}>
          {error}
        </div>
      )}

      {/* Create folder modal */}
      {showCreateFolderModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(0, 0, 0, 0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: 'white',
            padding: '2rem',
            borderRadius: '8px',
            minWidth: '400px',
            boxShadow: '0 10px 25px rgba(0, 0, 0, 0.15)'
          }}>
            <h3 style={{ marginTop: 0, marginBottom: '1rem', fontSize: '18px' }}>
              Create New Folder
            </h3>
            
            <input
              type="text"
              placeholder="Enter folder name"
              value={newFolderName}
              onChange={(e) => setNewFolderName(e.target.value)}
              onKeyPress={(e) => {
                if (e.key === 'Enter' && newFolderName.trim()) {
                  handleCreateFolderConfirm();
                }
                if (e.key === 'Escape') {
                  handleCreateFolderCancel();
                }
              }}
              autoFocus
              style={{
                width: '100%',
                padding: '0.75rem',
                fontSize: '14px',
                border: '1px solid #d1d5db',
                borderRadius: '6px',
                marginBottom: '1rem'
              }}
            />
            
            {error && (
              <div style={{
                color: '#dc2626',
                backgroundColor: '#fef2f2',
                border: '1px solid #fca5a5',
                borderRadius: '6px',
                padding: '0.5rem',
                marginBottom: '1rem',
                fontSize: '12px'
              }}>
                {error}
              </div>
            )}
            
            <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
              <button
                onClick={handleCreateFolderCancel}
                style={{
                  padding: '0.5rem 1rem',
                  backgroundColor: '#f3f4f6',
                  color: '#374151',
                  border: '1px solid #d1d5db',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                Cancel
              </button>
              
              <button
                onClick={handleCreateFolderConfirm}
                disabled={creatingFolder || !newFolderName.trim()}
                style={{
                  padding: '0.5rem 1rem',
                  backgroundColor: creatingFolder || !newFolderName.trim() ? '#e5e7eb' : '#10b981',
                  color: creatingFolder || !newFolderName.trim() ? '#6b7280' : 'white',
                  border: 'none',
                  borderRadius: '6px',
                  cursor: creatingFolder || !newFolderName.trim() ? 'not-allowed' : 'pointer',
                  fontSize: '14px'
                }}
              >
                {creatingFolder ? 'Creating...' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Current folder content */}
      {fileData.items.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '2rem', color: '#666' }}>
          {currentFolder ? 'This folder is empty.' : 'No files or folders found.'}
        </div>
      ) : (
        <>
          {/* Up navigation button */}
          {fileData.parentFolder !== null && (
            <div style={{ marginBottom: '1rem' }}>
              <button
                onClick={onNavigateUp}
                style={{
                  padding: '0.5rem 1rem',
                  backgroundColor: '#f3f4f6',
                  border: '1px solid #d1d5db',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                ← Back to {fileData.parentFolder === '' ? 'Home' : fileData.parentFolder}
              </button>
            </div>
          )}

          <ul style={{ listStyle: 'none', padding: 0 }}>
            {fileData.items.map(item => (
              <li 
                key={item.name} 
                style={{ 
                  display: 'flex', 
                  alignItems: 'center', 
                  marginBottom: 8, 
                  padding: '0.5rem',
                  backgroundColor: '#f9fafb',
                  borderRadius: '6px',
                  border: '1px solid #e5e7eb'
                }}
              >
                {/* Icon and name */}
                <div style={{ display: 'flex', alignItems: 'center', flex: 1 }}>
                  <span style={{ marginRight: '0.5rem', fontSize: '18px' }}>
                    {item.type === 'folder' ? '📁' : '📄'}
                  </span>
                  {item.type === 'folder' ? (
                    <button
                      onClick={() => {
                        if (searchTerm) {
                          // When searching, item.name is the full relative path
                          // Navigate to the folder by clearing search and setting the path
                          window.location.href = `${window.location.origin}${window.location.pathname}?folder=${encodeURIComponent(item.name)}`;
                        } else {
                          onNavigate(item.name);
                        }
                      }}
                      style={{
                        background: 'none',
                        border: 'none',
                        color: '#2563eb',
                        cursor: 'pointer',
                        textDecoration: 'underline',
                        fontSize: '14px',
                        padding: 0
                      }}
                    >
                      {item.name}
                    </button>
                  ) : (
                    <span style={{ fontSize: '14px' }}>{item.name}</span>
                  )}
                </div>

                {/* File info */}
                <div style={{ 
                  fontSize: '12px', 
                  color: '#6b7280', 
                  marginRight: '1rem',
                  minWidth: '120px',
                  textAlign: 'right'
                }}>
                  {item.type === 'file' && (
                    <div>{formatFileSize(item.size)}</div>
                  )}
                  <div>{formatDate(item.modifiedDate)}</div>
                </div>

                {/* Actions */}
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  {item.type === 'file' ? (
                    <>
                      <button 
                        onClick={() => handleDownload(item.name)} 
                        style={{ 
                          padding: '0.25rem 0.5rem',
                          fontSize: '12px',
                          backgroundColor: '#10b981',
                          color: 'white',
                          border: 'none',
                          borderRadius: '4px',
                          cursor: 'pointer'
                        }}
                      >
                        Download
                      </button>
                      <button 
                        onClick={() => handleDeleteFile(item.name)} 
                        style={{ 
                          padding: '0.25rem 0.5rem',
                          fontSize: '12px',
                          backgroundColor: '#ef4444',
                          color: 'white',
                          border: 'none',
                          borderRadius: '4px',
                          cursor: 'pointer'
                        }}
                      >
                        Delete
                      </button>
                    </>
                  ) : (
                    <button 
                      onClick={() => handleDeleteFolder(item.name)} 
                      style={{ 
                        padding: '0.25rem 0.5rem',
                        fontSize: '12px',
                        backgroundColor: '#ef4444',
                        color: 'white',
                        border: 'none',
                        borderRadius: '4px',
                        cursor: 'pointer'
                      }}
                    >
                      Delete
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
};

export default FileList;
