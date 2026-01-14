import { processService, type CommandResult } from './process.js';
import { config } from '../config.js';

export interface GitStatusFile {
  status: string;
  path: string;
}

export interface GitLogEntry {
  hash: string;
  author: string;
  date: string;
  message: string;
}

export class GitService {
  private projectRoot: string;

  constructor(projectRoot?: string) {
    this.projectRoot = projectRoot ?? config.projectRoot;
  }

  /**
   * Executes a git command.
   */
  private async git(args: string[]): Promise<CommandResult> {
    return processService.execute('git', args, { cwd: this.projectRoot });
  }

  /**
   * Gets the current git status.
   */
  async status(path?: string): Promise<{
    branch: string;
    staged: GitStatusFile[];
    unstaged: GitStatusFile[];
    untracked: GitStatusFile[];
  }> {
    const args = ['status', '--porcelain', '-b'];
    if (path) args.push('--', path);

    const result = await this.git(args);

    if (!result.success) {
      throw new Error(`Git status failed: ${result.stderr}`);
    }

    const lines = result.stdout.trim().split('\n').filter(Boolean);
    let branch = 'unknown';
    const staged: GitStatusFile[] = [];
    const unstaged: GitStatusFile[] = [];
    const untracked: GitStatusFile[] = [];

    for (const line of lines) {
      if (line.startsWith('##')) {
        // Branch line: ## branch...origin/branch
        const branchMatch = line.match(/^## ([^\s.]+)/);
        if (branchMatch) {
          branch = branchMatch[1];
        }
        continue;
      }

      const indexStatus = line[0];
      const workTreeStatus = line[1];
      const filePath = line.substring(3).trim();

      // Untracked files
      if (indexStatus === '?' && workTreeStatus === '?') {
        untracked.push({ status: 'untracked', path: filePath });
        continue;
      }

      // Staged changes
      if (indexStatus !== ' ' && indexStatus !== '?') {
        staged.push({ status: this.statusCodeToName(indexStatus), path: filePath });
      }

      // Unstaged changes
      if (workTreeStatus !== ' ' && workTreeStatus !== '?') {
        unstaged.push({ status: this.statusCodeToName(workTreeStatus), path: filePath });
      }
    }

    return { branch, staged, unstaged, untracked };
  }

  private statusCodeToName(code: string): string {
    const statusMap: Record<string, string> = {
      M: 'modified',
      A: 'added',
      D: 'deleted',
      R: 'renamed',
      C: 'copied',
      U: 'unmerged',
    };
    return statusMap[code] ?? code;
  }

  /**
   * Gets the git diff.
   */
  async diff(options?: { staged?: boolean; path?: string; base?: string }): Promise<string> {
    const args = ['diff'];

    if (options?.staged) {
      args.push('--staged');
    }

    if (options?.base) {
      args.push(options.base);
    }

    if (options?.path) {
      args.push('--', options.path);
    }

    const result = await this.git(args);

    if (!result.success && result.stderr) {
      throw new Error(`Git diff failed: ${result.stderr}`);
    }

    return result.stdout || 'No changes';
  }

  /**
   * Gets the commit log.
   */
  async log(options?: { limit?: number; oneline?: boolean; path?: string }): Promise<GitLogEntry[] | string> {
    const limit = options?.limit ?? 10;
    const args = ['log', `-${limit}`];

    if (options?.oneline) {
      args.push('--oneline');
      if (options?.path) {
        args.push('--', options.path);
      }
      const result = await this.git(args);
      if (!result.success) {
        throw new Error(`Git log failed: ${result.stderr}`);
      }
      return result.stdout;
    }

    args.push('--format=%H|%an|%ad|%s', '--date=short');
    if (options?.path) {
      args.push('--', options.path);
    }

    const result = await this.git(args);

    if (!result.success) {
      throw new Error(`Git log failed: ${result.stderr}`);
    }

    return result.stdout
      .trim()
      .split('\n')
      .filter(Boolean)
      .map((line) => {
        const [hash, author, date, ...messageParts] = line.split('|');
        return {
          hash,
          author,
          date,
          message: messageParts.join('|'),
        };
      });
  }

  /**
   * Gets branch information.
   */
  async branchInfo(includeRemote?: boolean): Promise<{
    current: string;
    local: string[];
    remote: string[];
  }> {
    const args = ['branch'];
    if (includeRemote) {
      args.push('-a');
    }

    const result = await this.git(args);

    if (!result.success) {
      throw new Error(`Git branch failed: ${result.stderr}`);
    }

    const lines = result.stdout.trim().split('\n').filter(Boolean);
    let current = '';
    const local: string[] = [];
    const remote: string[] = [];

    for (const line of lines) {
      const isCurrent = line.startsWith('*');
      const branchName = line.replace(/^\*?\s*/, '').trim();

      if (branchName.startsWith('remotes/')) {
        remote.push(branchName.replace('remotes/', ''));
      } else {
        local.push(branchName);
        if (isCurrent) {
          current = branchName;
        }
      }
    }

    return { current, local, remote };
  }
}

export const gitService = new GitService();
