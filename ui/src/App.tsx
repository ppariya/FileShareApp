import React, { useState, useEffect } from 'react';
import FileList from './components/FileList';

const API_BASE = process.env.REACT_APP_API_BASE || 'http://localhost:5109';

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

function App() {
  const [fileData, setFileData] = useState<FileListResponse>({ items: [], currentFolder: '', parentFolder: null });
  const [search, setSearch] = useState('');
  const [refresh, setRefresh] = useState(false);
  const [currentFolder, setCurrentFolder] = useState('');

  useEffect(() => {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (currentFolder) params.set('folder', currentFolder);
    
    fetch(`${API_BASE}/files?${params.toString()}`)
      .then(res => res.json())
      .then((data: FileListResponse) => {
        setFileData(data);
      })
      .catch(err => {
        console.error('Failed to fetch files:', err);
        setFileData({ items: [], currentFolder: '', parentFolder: null });
      });
  }, [search, refresh, currentFolder]);

  const navigateToFolder = (folderName: string) => {
    const newPath = currentFolder ? `${currentFolder}/${folderName}` : folderName;
    setCurrentFolder(newPath);
  };

  const navigateUp = () => {
    if (fileData.parentFolder !== null) {
      setCurrentFolder(fileData.parentFolder);
    }
  };

  const navigateToRoot = () => {
    setCurrentFolder('');
  };

  const createFolder = async (name: string) => {
    try {
      const response = await fetch(`${API_BASE}/files/folder`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, parentFolder: currentFolder || null })
      });
      
      if (response.ok) {
        setRefresh(r => !r);
      } else {
        const error = await response.text();
        alert(`Failed to create folder: ${error}`);
      }
    } catch (err) {
      alert(`Failed to create folder: ${err}`);
    }
  };

  return (
    <div style={{ maxWidth: 600, margin: '2rem auto', fontFamily: 'sans-serif' }}>
      <h1>File Sharing Service</h1>
      
      <div style={{ margin: '1rem 0' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <input
            type="text"
            placeholder="Search files and folders..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            style={{ flex: 1, padding: 8 }}
          />
          {search && (
            <button
              onClick={() => setSearch('')}
              style={{
                padding: '6px 12px',
                backgroundColor: '#ef4444',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              Clear
            </button>
          )}
        </div>
        {search && (
          <div style={{ 
            marginTop: '0.5rem', 
            padding: '0.5rem', 
            backgroundColor: '#e0f2fe', 
            borderRadius: '4px',
            fontSize: '14px',
            color: '#0369a1'
          }}>
            Searching for "{search}" in all folders...
          </div>
        )}
      </div>
      
      {/* Breadcrumb navigation */}
      <div style={{ marginBottom: '1rem', padding: '0.5rem', backgroundColor: '#f5f5f5', borderRadius: '4px' }}>
        <button onClick={navigateToRoot} style={{ marginRight: '0.5rem', padding: '0.25rem 0.5rem' }}>
          Home
        </button>
        {currentFolder && (
          <>
            {currentFolder.split('/').map((part, index, parts) => (
              <span key={index}>
                <span style={{ margin: '0 0.25rem' }}>/</span>
                <button 
                  onClick={() => setCurrentFolder(parts.slice(0, index + 1).join('/'))}
                  style={{ marginRight: '0.5rem', padding: '0.25rem 0.5rem' }}
                >
                  {part}
                </button>
              </span>
            ))}
          </>
        )}
      </div>
      
      <FileList 
        fileData={fileData}
        apiBase={API_BASE} 
        currentFolder={currentFolder}
        searchTerm={search}
        onDelete={() => setRefresh(r => !r)}
        onNavigate={navigateToFolder}
        onNavigateUp={navigateUp}
        onUpload={() => setRefresh(r => !r)}
        onCreateFolder={createFolder}
      />
    </div>
  );
}

export default App;
