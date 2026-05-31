import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { ProjectContext } from '../context/projectContextObject';
import { describe, it, expect, vi } from 'vitest';

describe('AppLayout Component', () => {
    it('renders the layout with dynamic sidebar menu when project is selected', () => {
        const mockContext = {
            projects: [],
            selectedProjectId: '1',
            selectedProject: {
                id: '1',
                name: 'Test Box',
                description: 'A box for testing',
                backlogCount: 0,
                sprintCount: 0,
                wikiCount: 0,
                docCount: 0,
                checkpointCount: 0,
                agentRunCount: 0,
                status: 0,
                createdAt: new Date().toISOString()
            },
            dashboard: null,
            backlog: [],
            sprints: [],
            knowledge: null,
            loading: false,
            error: '',
            setSelectedProjectId: vi.fn(),
            refreshProjects: vi.fn(),
            refreshProjectViews: vi.fn(),
            createProject: vi.fn(),
            updateProjectConfig: vi.fn()
        } as any;

        render(
            <ProjectContext.Provider value={mockContext}>
                <BrowserRouter>
                    <AppLayout />
                </BrowserRouter>
            </ProjectContext.Provider>
        );

        // Verifica renderização do Box Title no AppBar/Sidebar
        expect(screen.getByText('Test Box')).toBeInTheDocument();
        // Verifica itens do Menu Dinâmico
        expect(screen.getByText('Users')).toBeInTheDocument();
        expect(screen.getByText('Manager Flow')).toBeInTheDocument();
        expect(screen.getByText('Context-Box')).toBeInTheDocument();
        expect(screen.getByText('Memory-Box')).toBeInTheDocument();
        expect(screen.getByText('API Keys')).toBeInTheDocument();
    });

    it('renders no box selected state if no project is active', () => {
        const mockContext = {
            projects: [],
            selectedProjectId: '',
            selectedProject: null,
            dashboard: null,
            backlog: [],
            sprints: [],
            knowledge: null,
            loading: false,
            error: '',
            setSelectedProjectId: vi.fn(),
            refreshProjects: vi.fn(),
            refreshProjectViews: vi.fn(),
            createProject: vi.fn(),
            updateProjectConfig: vi.fn()
        } as any;

        render(
            <ProjectContext.Provider value={mockContext}>
                <BrowserRouter>
                    <AppLayout />
                </BrowserRouter>
            </ProjectContext.Provider>
        );

        expect(screen.getByText('No box selected')).toBeInTheDocument();
    });
});
