import { readFile, stat, readdir } from 'fs/promises';
import { resolve, normalize, relative, join } from 'path';
import { glob } from 'glob';
import { config } from '../config.js';

export class PathSecurityError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'PathSecurityError';
  }
}

export class FileSystemService {
  private projectRoot: string;

  constructor(projectRoot?: string) {
    this.projectRoot = normalize(projectRoot ?? config.projectRoot);
  }

  /**
   * Validates and resolves a path, ensuring it's within the project root.
   */
  validatePath(inputPath: string): string {
    // Resolve the path relative to project root
    const resolvedPath = normalize(resolve(this.projectRoot, inputPath));

    // Ensure the resolved path is within the project root
    const relativePath = relative(this.projectRoot, resolvedPath);

    if (relativePath.startsWith('..') || resolve(relativePath) === resolvedPath) {
      throw new PathSecurityError(
        `Path "${inputPath}" is outside the project root. Only paths within the project are allowed.`
      );
    }

    return resolvedPath;
  }

  /**
   * Reads a file and returns its contents.
   */
  async readFile(filePath: string, options?: { limit?: number; offset?: number }): Promise<string> {
    const validatedPath = this.validatePath(filePath);

    const stats = await stat(validatedPath);

    if (stats.isDirectory()) {
      throw new Error(`"${filePath}" is a directory, not a file.`);
    }

    if (stats.size > config.maxFileSize) {
      throw new Error(
        `File "${filePath}" is too large (${Math.round(stats.size / 1024 / 1024)}MB). Maximum allowed size is ${Math.round(config.maxFileSize / 1024 / 1024)}MB.`
      );
    }

    const content = await readFile(validatedPath, 'utf-8');

    // Apply offset and limit if specified
    if (options?.offset !== undefined || options?.limit !== undefined) {
      const lines = content.split('\n');
      const offset = options.offset ?? 0;
      const limit = options.limit ?? lines.length;
      const selectedLines = lines.slice(offset, offset + limit);

      // Add line numbers
      return selectedLines
        .map((line, index) => `${(offset + index + 1).toString().padStart(6)}  ${line}`)
        .join('\n');
    }

    return content;
  }

  /**
   * Lists the contents of a directory.
   */
  async listDirectory(dirPath: string): Promise<{ name: string; type: 'file' | 'directory' }[]> {
    const validatedPath = this.validatePath(dirPath);

    const stats = await stat(validatedPath);

    if (!stats.isDirectory()) {
      throw new Error(`"${dirPath}" is not a directory.`);
    }

    const entries = await readdir(validatedPath, { withFileTypes: true });

    return entries.map(entry => ({
      name: entry.name,
      type: entry.isDirectory() ? 'directory' : 'file',
    }));
  }

  /**
   * Finds files matching a glob pattern.
   */
  async globFiles(pattern: string, basePath?: string): Promise<string[]> {
    const searchPath = basePath ? this.validatePath(basePath) : this.projectRoot;

    const matches = await glob(pattern, {
      cwd: searchPath,
      nodir: true,
      ignore: [
        '**/node_modules/**',
        '**/bin/**',
        '**/obj/**',
        '**/.git/**',
        '**/dist/**',
      ],
    });

    // Return paths relative to project root
    return matches.map(match => {
      const fullPath = join(searchPath, match);
      return relative(this.projectRoot, fullPath);
    });
  }

  /**
   * Searches for content in files matching a pattern.
   */
  async searchCode(
    pattern: string,
    options?: { glob?: string; maxResults?: number }
  ): Promise<{ file: string; line: number; content: string }[]> {
    const filePattern = options?.glob ?? '**/*';
    const maxResults = options?.maxResults ?? 50;

    const files = await this.globFiles(filePattern);
    const results: { file: string; line: number; content: string }[] = [];
    const regex = new RegExp(pattern, 'gi');

    for (const file of files) {
      if (results.length >= maxResults) break;

      try {
        const fullPath = resolve(this.projectRoot, file);
        const content = await readFile(fullPath, 'utf-8');
        const lines = content.split('\n');

        for (let i = 0; i < lines.length; i++) {
          if (results.length >= maxResults) break;

          if (regex.test(lines[i])) {
            results.push({
              file,
              line: i + 1,
              content: lines[i].trim().substring(0, 200),
            });
            regex.lastIndex = 0; // Reset regex state
          }
        }
      } catch {
        // Skip files that can't be read (binary, permissions, etc.)
        continue;
      }
    }

    return results;
  }
}

export const fileSystemService = new FileSystemService();
