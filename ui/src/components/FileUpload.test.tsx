import React from 'react';
import '@testing-library/jest-dom';
import { render, fireEvent, waitFor, screen, act } from '@testing-library/react';
import FileUpload from './FileUpload';

// Mock fetch
const mockFetch = jest.fn();
beforeEach(() => {
  global.fetch = mockFetch;
  mockFetch.mockResolvedValue({
    ok: true,
    text: () => Promise.resolve(''),
    json: () => Promise.resolve({})
  });
});

afterEach(() => {
  jest.resetAllMocks();
});

describe('FileUpload', () => {
  const defaultProps = {
    apiBase: '/api',
    currentFolder: '',
    onUpload: jest.fn(),
    onCreateFolder: jest.fn()
  };

  describe('Component Rendering', () => {
    it('renders all main sections', () => {
      render(<FileUpload {...defaultProps} />);
      
      expect(screen.getByText('Create New Folder')).toBeInTheDocument();
      expect(screen.getByText(/Upload a file/)).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Folder name')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Rename file before upload (optional)')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /create/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /upload/i })).toBeInTheDocument();
    });

    it('displays current folder indicator when in a folder', () => {
      render(<FileUpload {...defaultProps} currentFolder="documents/photos" />);
      
      expect(screen.getByText('Uploading to: documents/photos')).toBeInTheDocument();
    });

    it('does not display folder indicator when in root', () => {
      render(<FileUpload {...defaultProps} currentFolder="" />);
      
      expect(screen.queryByText(/Uploading to:/)).not.toBeInTheDocument();
    });

    it('displays file upload rules', () => {
      render(<FileUpload {...defaultProps} />);
      
      expect(screen.getByText(/Only one file can be uploaded at a time/)).toBeInTheDocument();
      expect(screen.getByText(/Filenames must be 1-255 characters/)).toBeInTheDocument();
      expect(screen.getByText(/Uploading a file with the same name/)).toBeInTheDocument();
      expect(screen.getByText(/Partial uploads are cleaned up/)).toBeInTheDocument();
    });
  });

  describe('Folder Creation', () => {
    it('calls onCreateFolder when creating a folder', async () => {
      const onCreateFolder = jest.fn();
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'new-folder' } });
      fireEvent.click(createButton);
      
      await waitFor(() => expect(onCreateFolder).toHaveBeenCalledWith('new-folder'));
    });

    it('trims whitespace from folder name', async () => {
      const onCreateFolder = jest.fn();
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: '  folder-with-spaces  ' } });
      fireEvent.click(createButton);
      
      await waitFor(() => expect(onCreateFolder).toHaveBeenCalledWith('folder-with-spaces'));
    });

    it('disables create button when folder name is empty', () => {
      render(<FileUpload {...defaultProps} />);
      
      const createButton = screen.getByRole('button', { name: /create/i });
      expect(createButton).toBeDisabled();
    });

    it('disables create button when folder name is only whitespace', () => {
      render(<FileUpload {...defaultProps} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: '   ' } });
      expect(createButton).toBeDisabled();
    });

    it('enables create button when folder name is provided', () => {
      render(<FileUpload {...defaultProps} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'test-folder' } });
      expect(createButton).not.toBeDisabled();
    });

    it('shows creating state during folder creation', async () => {
      const onCreateFolder = jest.fn(() => new Promise(resolve => setTimeout(resolve, 100)));
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'test-folder' } });
      fireEvent.click(createButton);
      
      expect(screen.getByRole('button', { name: /creating.../i })).toBeInTheDocument();
      expect(createButton).toBeDisabled();
      
      await waitFor(() => expect(onCreateFolder).toHaveBeenCalled());
    });

    it('clears folder name after successful creation', async () => {
      const onCreateFolder = jest.fn().mockResolvedValue(undefined);
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'test-folder' } });
      fireEvent.click(createButton);
      
      await waitFor(() => expect(onCreateFolder).toHaveBeenCalled());
      
      await act(async () => {
        // Wait for state updates to complete
        await new Promise(resolve => setTimeout(resolve, 0));
      });
      
      expect(folderInput).toHaveValue('');
    });

    it('displays error when folder creation fails', async () => {
      const onCreateFolder = jest.fn().mockRejectedValue(new Error('Folder already exists'));
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'test-folder' } });
      fireEvent.click(createButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('Folder already exists')).toBeInTheDocument();
      });
    });
  });

  describe('File Upload', () => {
    const createMockFile = (name: string, size: number, type = 'text/plain') => {
      return new File([new ArrayBuffer(size)], name, { type });
    };

    it('displays selected file name in rename field', () => {
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const renameInput = screen.getByPlaceholderText(/rename file before upload/i);
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      
      expect(renameInput).toHaveValue('test.txt');
    });

    it('uploads file successfully', async () => {
      const onUpload = jest.fn();
      render(<FileUpload {...defaultProps} onUpload={onUpload} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith('/api/files/upload', {
          method: 'POST',
          body: expect.any(FormData)
        });
        expect(onUpload).toHaveBeenCalled();
      });
    });

    it('uploads file with custom name', async () => {
      const onUpload = jest.fn();
      render(<FileUpload {...defaultProps} onUpload={onUpload} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const renameInput = screen.getByPlaceholderText(/rename file before upload/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.change(renameInput, { target: { value: 'renamed-file.txt' } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        const formData = mockFetch.mock.calls[0][1].body;
        expect(formData.get('newName')).toBe('renamed-file.txt');
        expect(onUpload).toHaveBeenCalled();
      });
    });

    it('includes folder path when uploading to subfolder', async () => {
      const onUpload = jest.fn();
      render(<FileUpload {...defaultProps} currentFolder="documents" onUpload={onUpload} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        const formData = mockFetch.mock.calls[0][1].body;
        expect(formData.get('folderPath')).toBe('documents');
      });
    });

    it('does nothing when no file is selected', async () => {
      render(<FileUpload {...defaultProps} />);
      
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      fireEvent.click(uploadButton);
      
      expect(mockFetch).not.toHaveBeenCalled();
    });

    it('shows error when multiple files are selected', async () => {
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const files = [createMockFile('test1.txt', 100), createMockFile('test2.txt', 100)];
      Object.defineProperty(fileInput, 'files', {
        value: files,
        writable: false,
      });
      fireEvent.change(fileInput);
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('You can upload only 1 file at a time.')).toBeInTheDocument();
      });
    });

    it('shows error when file is empty', async () => {
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('empty.txt', 0);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('File is empty.')).toBeInTheDocument();
      });
    });

    it('shows error when file exceeds size limit', async () => {
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('large.txt', 101 * 1024 * 1024); // 101MB
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText("File 'large.txt' exceeds the 100MB size limit.")).toBeInTheDocument();
      });
    });

    it('shows uploading state during upload', async () => {
      mockFetch.mockImplementation(() => new Promise(resolve => setTimeout(() => resolve({ ok: true }), 100)));
      
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      expect(screen.getByRole('button', { name: /uploading.../i })).toBeInTheDocument();
      expect(uploadButton).toBeDisabled();
      
      await waitFor(() => expect(mockFetch).toHaveBeenCalled());
    });

    it('clears file input and rename field after successful upload', async () => {
      const onUpload = jest.fn();
      render(<FileUpload {...defaultProps} onUpload={onUpload} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const renameInput = screen.getByPlaceholderText(/rename file before upload/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.change(renameInput, { target: { value: 'custom-name.txt' } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(onUpload).toHaveBeenCalled();
        expect((fileInput as HTMLInputElement).value).toBe('');
        expect(renameInput).toHaveValue('');
      });
    });

    it('handles upload error with JSON response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        text: () => Promise.resolve('{"message": "File already exists"}')
      });
      
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('File already exists')).toBeInTheDocument();
      });
    });

    it('handles upload error with plain text response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        text: () => Promise.resolve('Server error occurred')
      });
      
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('Server error occurred')).toBeInTheDocument();
      });
    });

    it('handles network error during upload', async () => {
      mockFetch.mockRejectedValue(new Error('Network error'));
      
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });

    it('handles upload error with empty response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        text: () => Promise.resolve('')
      });
      
      render(<FileUpload {...defaultProps} />);
      
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('Upload failed')).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    const createMockFile = (name: string, size: number, type = 'text/plain') => {
      return new File([new ArrayBuffer(size)], name, { type });
    };

    it('clears error when starting new upload', async () => {
      render(<FileUpload {...defaultProps} />);
      
      // First, create an error
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      fireEvent.change(folderInput, { target: { value: 'test' } });
      fireEvent.click(createButton);
      
      // Wait for error to appear
      await waitFor(() => {
        expect(screen.queryByTestId('upload-error')).not.toBeInTheDocument();
      });
      
      // Now start an upload - error should be cleared
      const fileInput = screen.getByLabelText(/upload a file/i);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      
      const file = createMockFile('test.txt', 100);
      fireEvent.change(fileInput, { target: { files: [file] } });
      fireEvent.click(uploadButton);
      
      // Error should be cleared immediately when upload starts
      expect(screen.queryByTestId('upload-error')).not.toBeInTheDocument();
    });

    it('clears error when starting new folder creation', async () => {
      const onCreateFolder = jest.fn().mockRejectedValueOnce(new Error('First error')).mockResolvedValueOnce(undefined);
      render(<FileUpload {...defaultProps} onCreateFolder={onCreateFolder} />);
      
      const folderInput = screen.getByPlaceholderText(/folder name/i);
      const createButton = screen.getByRole('button', { name: /create/i });
      
      // First attempt - should fail
      fireEvent.change(folderInput, { target: { value: 'test' } });
      fireEvent.click(createButton);
      
      await waitFor(() => {
        expect(screen.getByTestId('upload-error')).toBeInTheDocument();
        expect(screen.getByText('First error')).toBeInTheDocument();
      });
      
      // Second attempt - error should be cleared immediately
      fireEvent.change(folderInput, { target: { value: 'test2' } });
      fireEvent.click(createButton);
      
      expect(screen.queryByTestId('upload-error')).not.toBeInTheDocument();
    });
  });
});
