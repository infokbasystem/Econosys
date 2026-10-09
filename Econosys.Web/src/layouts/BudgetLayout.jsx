import { useCallback, useEffect, useMemo, useState } from "react";
import { Outlet } from "react-router-dom";
import {
    BarChart3,
    FilePlus,
    PiggyBank,
    ClockIcon,
} from "lucide-react";
import Header from "../components/Header";
import LeftMenu from "../components/LeftMenu";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";
import apiClient from "../config/apiClient";
import { getSharedRequest } from "../helpers/sharedRequest";

const getBudgetLabel = (budget) => {
    const name = budget.name?.trim();
    const year = budget.year != null ? String(budget.year) : '';

    // if (name && year && name !== year) {
    //     return `${name} - ${year}`;
    // }

    return name || year || `Budget ${budget.id}`;
};

const menuGroups = [
    {
        items: [
            { to: "/budget", label: "Aktuell budget", icon: PiggyBank, end: true },
            { to: "/budget/new", label: "Skapa ny budget", icon: FilePlus },
        ],
    },
    {
        items: [
            { to: "/budget/salesreport", label: "Säljrapport", icon: BarChart3 },
        ],
    },
];

const BudgetLayout = () => {
    const [budgets, setBudgets] = useState([]);
    const [budgetListVersion, setBudgetListVersion] = useState(0);

    useEffect(() => {
        let isActive = true;

        getSharedRequest(`budget-layout-list:${budgetListVersion}`, () => apiClient.get('/budget'))
            .then((response) => {
                if (isActive) {
                    setBudgets(Array.isArray(response.data) ? response.data : []);
                }
            })
            .catch((error) => {
                console.error('Failed to load budgets', error);
            });

        return () => {
            isActive = false;
        };
    }, [budgetListVersion]);

    const refreshBudgets = useCallback(() => {
        setBudgetListVersion((prev) => prev + 1);
    }, []);

    const outletContext = useMemo(() => ({ refreshBudgets }), [refreshBudgets]);

    const groups = budgets.length === 0
        ? menuGroups
        : [
            ...menuGroups,
            {
                label: "Tidigare budgetar",
                icon: ClockIcon,
                items: budgets.map((budget) => ({
                    to: `/budget/${budget.id}`,
                    label: getBudgetLabel(budget),
                    paddingLeft: 'pl-10.5',
                })),
            },
        ];

    const menuContent = <LeftMenu groups={groups} showGroupLabels />;

    return (
        <PdfProvider>
            <div className="relative flex h-screen min-h-0 w-screen min-w-0 flex-col overflow-x-hidden overflow-y-auto bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                <div className="shrink-0">
                    <Header />
                </div>
                <div className="sticky top-0 z-50 shrink-0">
                    <Navbar />
                </div>
                <div className="relative min-w-0 grow">
                    <div className="flex min-w-0 items-start md:px-[clamp(4px,3vw,3vw)]">
                        <div className="mt-10 w-60 shrink-0 self-stretch border-r border-gray-300">
                            <div className="sticky top-[calc(66px+1rem)] mb-20 flex max-h-[calc(100vh-66px-2rem)] flex-col overflow-y-auto">
                                {menuContent}
                            </div>
                        </div>

                        <div className="flex-grow min-w-0 pt-4 px-0 relative">
                            <div className="outlet-leading-none pt-0">
                                <Outlet context={outletContext} />
                            </div>
                        </div>
                    </div>

                    <PdfPanel />
                </div>
            </div>
        </PdfProvider>
    )
}

export default BudgetLayout
