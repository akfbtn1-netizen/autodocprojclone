import { spawn } from 'child_process';
import { config } from '../config.js';

export interface CommandResult {
  stdout: string;
  stderr: string;
  exitCode: number;
  success: boolean;
}

export class ProcessService {
  private projectRoot: string;
  private timeout: number;

  constructor(projectRoot?: string, timeout?: number) {
    this.projectRoot = projectRoot ?? config.projectRoot;
    this.timeout = timeout ?? config.commandTimeout;
  }

  /**
   * Executes a command and returns the result.
   */
  async execute(
    command: string,
    args: string[],
    options?: {
      cwd?: string;
      timeout?: number;
      env?: Record<string, string>;
    }
  ): Promise<CommandResult> {
    return new Promise((resolve) => {
      const cwd = options?.cwd ?? this.projectRoot;
      const timeout = options?.timeout ?? this.timeout;

      const child = spawn(command, args, {
        cwd,
        shell: true,
        env: { ...process.env, ...options?.env },
      });

      let stdout = '';
      let stderr = '';

      child.stdout.on('data', (data) => {
        stdout += data.toString();
      });

      child.stderr.on('data', (data) => {
        stderr += data.toString();
      });

      const timer = setTimeout(() => {
        child.kill('SIGTERM');
        resolve({
          stdout,
          stderr: stderr + '\n[Command timed out]',
          exitCode: -1,
          success: false,
        });
      }, timeout);

      child.on('close', (exitCode) => {
        clearTimeout(timer);
        resolve({
          stdout,
          stderr,
          exitCode: exitCode ?? 0,
          success: exitCode === 0,
        });
      });

      child.on('error', (error) => {
        clearTimeout(timer);
        resolve({
          stdout,
          stderr: stderr + '\n' + error.message,
          exitCode: -1,
          success: false,
        });
      });
    });
  }

  /**
   * Executes a PowerShell script.
   */
  async executePowerShell(
    scriptPath: string,
    args: string[] = [],
    options?: { cwd?: string; timeout?: number }
  ): Promise<CommandResult> {
    return this.execute('pwsh', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath, ...args], options);
  }

  /**
   * Executes a dotnet command.
   */
  async executeDotnet(
    subcommand: string,
    args: string[] = [],
    options?: { cwd?: string; timeout?: number }
  ): Promise<CommandResult> {
    return this.execute('dotnet', [subcommand, ...args], options);
  }

  /**
   * Executes an npm command.
   */
  async executeNpm(
    subcommand: string,
    args: string[] = [],
    options?: { cwd?: string; timeout?: number }
  ): Promise<CommandResult> {
    return this.execute('npm', [subcommand, ...args], options);
  }
}

export const processService = new ProcessService();
