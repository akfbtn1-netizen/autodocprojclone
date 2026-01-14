import { readFile } from 'fs/promises';
import { resolve, join } from 'path';
import { glob } from 'glob';
import { config } from '../config.js';

export interface SkillMetadata {
  name: string;
  description: string;
}

export interface LoadedSkill {
  metadata: SkillMetadata;
  content: string;
  references: Map<string, string>;
}

/**
 * Parses frontmatter from a SKILL.md file.
 */
function parseFrontmatter(content: string): { metadata: SkillMetadata; body: string } {
  const frontmatterMatch = content.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);

  if (!frontmatterMatch) {
    return {
      metadata: { name: 'unknown', description: '' },
      body: content,
    };
  }

  const frontmatter = frontmatterMatch[1];
  const body = frontmatterMatch[2];

  // Parse YAML-like frontmatter (simple key: value pairs)
  const metadata: SkillMetadata = { name: 'unknown', description: '' };

  const nameMatch = frontmatter.match(/^name:\s*(.+)$/m);
  if (nameMatch) {
    metadata.name = nameMatch[1].trim();
  }

  // Handle multi-line description with | or simple single-line
  const descMatch = frontmatter.match(/^description:\s*\|?\s*\n?([\s\S]*?)(?=^[a-z]+:|$)/m);
  if (descMatch) {
    metadata.description = descMatch[1].trim().replace(/\n\s+/g, ' ');
  } else {
    const simpleDescMatch = frontmatter.match(/^description:\s*(.+)$/m);
    if (simpleDescMatch) {
      metadata.description = simpleDescMatch[1].trim();
    }
  }

  return { metadata, body };
}

/**
 * Loads a skill from its SKILL.md file and associated references.
 */
export async function loadSkill(skillPath: string): Promise<LoadedSkill> {
  const skillContent = await readFile(skillPath, 'utf-8');
  const { metadata, body } = parseFrontmatter(skillContent);

  // Load reference files from references/ subdirectory
  const skillDir = resolve(skillPath, '..');
  const referencesDir = join(skillDir, 'references');
  const references = new Map<string, string>();

  try {
    const refFiles = await glob('*.md', { cwd: referencesDir });

    for (const refFile of refFiles) {
      try {
        const refContent = await readFile(join(referencesDir, refFile), 'utf-8');
        references.set(refFile, refContent);
      } catch {
        // Skip files that can't be read
      }
    }
  } catch {
    // No references directory, that's fine
  }

  return {
    metadata,
    content: body,
    references,
  };
}

/**
 * Loads multiple skills and combines them.
 */
export async function loadSkills(skillPaths: string[]): Promise<LoadedSkill> {
  const skills = await Promise.all(skillPaths.map(loadSkill));

  // Combine all skills
  const combined: LoadedSkill = {
    metadata: {
      name: skills.map((s) => s.metadata.name).join(' + '),
      description: skills.map((s) => s.metadata.description).join(' | '),
    },
    content: skills.map((s) => s.content).join('\n\n---\n\n'),
    references: new Map(),
  };

  // Merge all references
  for (const skill of skills) {
    for (const [name, content] of skill.references) {
      combined.references.set(`${skill.metadata.name}/${name}`, content);
    }
  }

  return combined;
}

/**
 * Skill file paths configuration.
 */
export const skillPaths = {
  frontend: resolve(config.skillsPath, 'senior-frontend', 'senior-frontend', 'SKILL.md'),
  apiIntegration: resolve(config.skillsPath, 'api-integration-specialist', 'SKILL.md'),
  e2eTesting: resolve(config.skillsPath, 'e2e-testing-patterns', 'SKILL.md'),
  agentArchitecture: [
    resolve(config.skillsPath, 'ai-agent-architecture', 'SKILL.md'),
    resolve(config.skillsPath, 'agentic-rag-implementation', 'SKILL.md'),
  ],
};

// Cache for loaded skills
const skillCache = new Map<string, LoadedSkill>();

/**
 * Gets a skill, using cache if available.
 */
export async function getSkill(name: keyof typeof skillPaths): Promise<LoadedSkill> {
  if (skillCache.has(name)) {
    return skillCache.get(name)!;
  }

  const path = skillPaths[name];
  const skill = Array.isArray(path) ? await loadSkills(path) : await loadSkill(path);

  skillCache.set(name, skill);
  return skill;
}

/**
 * Clears the skill cache (useful for development).
 */
export function clearSkillCache(): void {
  skillCache.clear();
}
