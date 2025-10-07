import React, { useRef, useState } from 'react';

interface FileUploadProps {
  apiBase: string;
  currentFolder: string;
  onUpload: () => void;
  onCreateFolder: (name: string) => void;
}

const FileUpload: React.FC<FileUploadProps> = ({ apiBase, currentFolder, onUpload, onCreateFolder }) => {
  const fileInput = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const [rename, setRename] = useState('');
  const [newFolderName, setNewFolderName] = useState('');
  const [creatingFolder, setCreatingFolder] = useState(false);

  const handleUpload = async () => {
    if (!fileInput.current?.files?.length) return;
    if (fileInput.current.files.length > 1) {
      setError('You can upload only 1 file at a time.');
      return;
    }
    const maxFileSize = 100 * 1024 * 1024; // 100MB
    const file = fileInput.current.files[0];
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
    if (rename.trim()) {
      formData.append('newName', rename.trim());
    }
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
          // Try to get error message from backend
          const text = await res.text();
          if (text) {
            // Try to parse JSON, fallback to plain text
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
      if (fileInput.current) fileInput.current.value = '';
      setRename('');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setUploading(false);
    }
  };

  const handleCreateFolder = async () => {
    if (!newFolderName.trim()) return;
    setCreatingFolder(true);
    setError('');
    try {
      await onCreateFolder(newFolderName.trim());
      setNewFolderName('');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setCreatingFolder(false);
    }
  };

  return (
    <div
      style={{
        maxWidth: 420,
        margin: '2rem auto',
        background: '#fff',
        borderRadius: 12,
        boxShadow: '0 2px 16px rgba(0,0,0,0.07)',
        padding: '2rem 2rem 1.5rem 2rem',
        border: '1px solid #eee',
      }}
    >
      {/* Current folder indicator */}
      {currentFolder && (
        <div style={{ 
          marginBottom: '1rem', 
          padding: '0.5rem', 
          backgroundColor: '#f0f9ff', 
          borderRadius: '6px',
          fontSize: '14px',
          color: '#1e40af'
        }}>
          Uploading to: {currentFolder}
        </div>
      )}

      {/* Folder creation section */}
      <div style={{ marginBottom: '2rem' }}>
        <label style={{ fontWeight: 600, fontSize: 16, color: '#222', display: 'block', marginBottom: 8 }}>
          Create New Folder
        </label>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <input
            type="text"
            placeholder="Folder name"
            value={newFolderName}
            onChange={e => setNewFolderName(e.target.value)}
            style={{
              flex: 1,
              padding: '8px 10px',
              fontSize: 15,
              border: '1px solid #ccc',
              borderRadius: 6,
              background: '#f7f8fa',
            }}
          />
          <button
            onClick={handleCreateFolder}
            disabled={creatingFolder || !newFolderName.trim()}
            style={{
              padding: '8px 16px',
              fontSize: 14,
              fontWeight: 600,
              background: creatingFolder || !newFolderName.trim() ? '#e0e0e0' : '#10b981',
              color: creatingFolder || !newFolderName.trim() ? '#888' : '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: creatingFolder || !newFolderName.trim() ? 'not-allowed' : 'pointer',
            }}
          >
            {creatingFolder ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>

      {/* File upload section */}
      <label htmlFor="file-upload-input" style={{ fontWeight: 600, fontSize: 18, color: '#222', display: 'block', marginBottom: 12 }}>
        Upload a file <span style={{ color: '#888', fontWeight: 400, fontSize: 15 }}>(max 100MB)</span>
      </label>
      <input
        id="file-upload-input"
        type="file"
        ref={fileInput}
        onChange={e => {
          const file = e.target.files && e.target.files[0];
          setRename(file ? file.name : '');
        }}
        style={{
          display: 'block',
          marginBottom: 12,
          padding: '8px 0',
          fontSize: 15,
          border: '1px solid #ccc',
          borderRadius: 6,
          background: '#fafbfc',
          width: '100%',
        }}
      />
      <input
        type="text"
        placeholder="Rename file before upload (optional)"
        value={rename}
        onChange={e => setRename(e.target.value)}
        style={{
          display: 'block',
          marginBottom: 16,
          padding: '8px 10px',
          fontSize: 15,
          border: '1px solid #ccc',
          borderRadius: 6,
          background: '#f7f8fa',
          width: '100%',
        }}
      />
      <ul style={{
        color: '#666',
        fontSize: 14,
        background: '#f7f8fa',
        borderRadius: 8,
        padding: '12px 18px',
        margin: 0,
        marginBottom: 18,
        listStyle: 'disc inside',
        boxShadow: '0 1px 4px rgba(0,0,0,0.03)',
      }}>
        <li>Only one file can be uploaded at a time. File must be 100MB or less.</li>
        <li>Filenames must be 1-255 characters, and cannot contain <code>/</code>, <code>\</code>, <code>..</code>, or special characters.</li>
        <li>Uploading a file with the same name as an existing file is not allowed.</li>
        <li>Partial uploads are cleaned up automatically if an error occurs.</li>
      </ul>
      <button
        onClick={handleUpload}
        disabled={uploading}
        style={{
          width: '100%',
          padding: '12px 0',
          fontSize: 16,
          fontWeight: 600,
          background: uploading ? '#e0e0e0' : '#2563eb',
          color: uploading ? '#888' : '#fff',
          border: 'none',
          borderRadius: 6,
          boxShadow: uploading ? 'none' : '0 1px 4px rgba(37,99,235,0.08)',
          cursor: uploading ? 'not-allowed' : 'pointer',
          transition: 'background 0.2s',
          marginBottom: 8,
        }}
      >
        {uploading ? 'Uploading...' : 'Upload'}
      </button>
      {error && (
        <div
          data-testid="upload-error"
          style={{
            color: '#e11d48',
            background: '#fef2f2',
            border: '1px solid #fca5a5',
            borderRadius: 6,
            padding: '8px 12px',
            marginTop: 8,
            fontSize: 15,
          }}
        >
          {error}
        </div>
      )}
    </div>
  );
};

export default FileUpload;
